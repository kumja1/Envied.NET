using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Envied.SourceGenerator.Common.Models.Config;
using Envied.SourceGenerator.Common.Utils;
using Envied.SourceGenerator.Models.TypeInfo;
using Envied.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = Envied.SourceGenerator.Models.TypeInfo.TypeInfo;

namespace Envied.SourceGenerator;

internal partial class EnviedSourceGenerator
{
    private static readonly Regex InterpolationPattern = new(
        @"\$\{([^}]+)\}",
        RegexOptions.Compiled
    );
    private static readonly Regex QuoteRegex = new(@"""+", RegexOptions.Compiled);
    private static readonly Regex EscapeSequenceRegex = new(
        @"(?<!\\)(\\\\)*(\\[\\""abfnrtv]|\\u[0-9a-fA-F]{4}|\\U[0-9a-fA-F]{8})",
        RegexOptions.Compiled
    );

    private static readonly Aes Aes = Aes.Create();

    private static ClassInfo TransformClass(
        ClassDeclarationSyntax classSyntax,
        ISymbol classSymbol,
        SemanticModel semanticModel,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();

        var attributeSyntax = classSyntax
            .AttributeLists.SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "Envied");
        token.ThrowIfCancellationRequested();

        if (attributeSyntax == null)
            return ClassInfo.Empty;

        var config = EnviedConfig.From(attributeSyntax);
        token.ThrowIfCancellationRequested();
        var env = LoadEnvironment(config, semanticModel);

        if (env == null)
            return ClassInfo.Empty;
        token.ThrowIfCancellationRequested();

        var properties = new List<PropertyInfo>();
        foreach (var property in classSyntax.Members.OfType<PropertyDeclarationSyntax>())
        {
            token.ThrowIfCancellationRequested();
            var transformedProperty = TransformProperty(property, semanticModel, config, env);
            if (transformedProperty != null)
            {
                properties.Add(transformedProperty.Value);
            }
        }

        return new ClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            Modifiers = classSyntax.Modifiers.Select(m => m.Text).ToArray(),
            Properties = properties,
        };
    }

    private static Dictionary<string, string>? LoadEnvironment(
        EnviedConfig config,
        SemanticModel semanticModel
    )
    {
        var projectRoot = Path.GetDirectoryName(semanticModel.SyntaxTree.FilePath);
        if (string.IsNullOrEmpty(projectRoot))
            return null;

        var envPath = Path.Combine(projectRoot, config.Path);
        return EnviromentHelper.LoadEnvironment(envPath);
    }

    private static PropertyInfo? TransformProperty(
        PropertyDeclarationSyntax property,
        SemanticModel semanticModel,
        EnviedConfig config,
        Dictionary<string, string> env
    )
    {
        var propertySymbol = semanticModel.GetDeclaredSymbol(property);
        if (propertySymbol == null)
            return null;

        var modifiers = property.Modifiers.Select(m => m.Text).ToArray();

        var typeInfo = semanticModel.GetTypeInfo(property.Type);

        if (typeInfo.Type == null)
            return null;

        var namedType = (INamedTypeSymbol)typeInfo.Type;
        var fieldName = property.Identifier.Text;
        var fieldAttribute = property
            .AttributeLists.SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "EnviedField");

        if (fieldAttribute == null)
            return null;

        var fieldConfig = EnviedFieldConfig.From(fieldAttribute, config);

        var envName = fieldConfig.Name ?? fieldName;
        if (fieldConfig.UseConstantCase)
            envName = envName.ToUpper();

        var value = GetValue(
            envName,
            env,
            config.Environment,
            fieldConfig.DefaultValue,
            namedType,
            fieldConfig.Optional
        );

        if (value == null)
            return null;

        if (property.Type is PredefinedTypeSyntax { Keyword.Text: "string" })
        {
            if (fieldConfig.Interpolate)
                value = InterpolateValue(value, fieldConfig, env);

            if (fieldConfig is { RawString: true, Obfuscate: false })
                value = EscapeString(value, fieldConfig.RawString);
        }

        var fieldValue = fieldConfig.Obfuscate
            ? ObfuscateField(
                value,
                fieldConfig.RandomSeed,
                KeyHelper.DeriveKey(semanticModel.Compilation.Assembly)
            )
            : value;

        return new PropertyInfo
        {
            Name = fieldName,
            Type = TypeInfo.From(namedType),
            Value = TypeHelper.GetConversionExpression(
                $"\"{fieldValue}\"",
                namedType
            ),
            Modifiers = modifiers,
            IsObfuscated = fieldConfig.Obfuscate,
        };
    }

    private static string? GetValue(
        string envName,
        Dictionary<string, string> env,
        bool environment,
        object? defaultValue,
        INamedTypeSymbol namedType,
        bool optional
    )
    {
        if (!env.TryGetValue(envName, out string? value))
        {
            value = Environment.GetEnvironmentVariable(envName);
            if (string.IsNullOrEmpty(value) && defaultValue != null)
            {
                value = defaultValue switch
                {
                    bool b => b.ToString().ToLower(),
                    string s => s,
                    _ => defaultValue.ToString() ?? string.Empty,
                };
            }

            if (string.IsNullOrEmpty(value))
            {
                Console.WriteLine(
                    $"Warning: Environment variable '{envName}' not found. Default value: '{defaultValue}'."
                );
                if (!optional || namedType.IsValueType && namedType.Name != "Nullable")
                    return null;
                value = string.Empty;
            }
        }

        if (environment && !string.IsNullOrEmpty(value))
        {
            var envValue = Environment.GetEnvironmentVariable(value);
            if (!string.IsNullOrEmpty(envValue))
                value = envValue;
        }

        if (string.IsNullOrEmpty(value) || TypeHelper.IsValidTypeConversion(value, namedType))
            return value;

        return null;
    }

    private static string InterpolateValue(
        string value,
        EnviedFieldConfig config,
        Dictionary<string, string> env
    )
    {
        return InterpolationPattern.Replace(
            value,
            match =>
            {
                var envName = match.Groups[1].Value.Trim();
                if (config.UseConstantCase)
                    envName = envName.ToUpper();

                if (env.TryGetValue(envName, out var replacement) || config.Optional)
                    return replacement;

                return match.Value;
            }
        );
    }

    private static string EscapeString(string value, bool rawString)
    {
        if (!rawString)
        {
            return EscapeSequenceRegex.Replace(
                value,
                match =>
                {
                    if (match.Value.StartsWith("\\"))
                        return match.Value;
                    return match.Value.Replace("\"", "\\\"");
                }
            );
        }

        int maxQuotes = QuoteRegex
            .Matches(value)
            .Cast<Match>()
            .Select(m => m.Length)
            .DefaultIfEmpty(0)
            .Max();

        int delimiterQuotes = Math.Max(3, maxQuotes + 1);
        string delimiter = new('"', delimiterQuotes);
        string escapedValue = value.Replace(delimiter, $"{delimiter}\"");

        return $"""
            {delimiter}
            {escapedValue}
            {delimiter}
            """;
    }

    private static string ObfuscateField(string value, int seed, byte[] key)
    {
        Aes.Key = key;
        var iv = new byte[16];
        var random = seed != 0 ? new Random(seed) : new Random();
        random.NextBytes(iv);
        Aes.IV = iv;

        using var encryptor = Aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(
            Encoding.UTF8.GetBytes(value),
            0,
            value.Length
        );

        Aes.Clear();
        return Convert.ToBase64String([.. iv.Concat(encrypted)]);
    }
}
