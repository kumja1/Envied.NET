using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Envied.SourceGenerator.Models;
using Envied.SourceGenerator.Models.TypeInfo;
using TypeInfo = Envied.SourceGenerator.Models.TypeInfo.TypeInfo;
using Envied.SourceGenerator.Models.Config;
using Envied.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using dotenv.net;

namespace Envied.SourceGenerator;

internal static class Exec
{
    private static readonly Regex InterpolationPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"""+", RegexOptions.Compiled);
    private static readonly Regex EscapeSequenceRegex = new(
        @"(?<!\\)(\\\\)*(\\[\\""abfnrtv]|\\u[0-9a-fA-F]{4}|\\U[0-9a-fA-F]{8})",
        RegexOptions.Compiled
    );

    private static readonly Aes Aes = Aes.Create();

    internal static ClassInfo TransformClass(
        ClassDeclarationSyntax classSyntax,
        ISymbol classSymbol,
        SemanticModel semanticModel,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var classInfo = ClassInfo.Empty;
        var diagnostics = new List<DiagnosticInfo>();

        var attributeSyntax = classSyntax.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "Envied");
        token.ThrowIfCancellationRequested();

        if (attributeSyntax == null)
            return classInfo with { Diagnostics = diagnostics };

        var config = EnviedConfig.From(attributeSyntax);

        token.ThrowIfCancellationRequested();
        var env = LoadEnvironment(config, semanticModel, diagnostics, classSyntax.GetLocation());

        var properties = new List<PropertyInfo>();
        foreach (var property in classSyntax.Members.OfType<PropertyDeclarationSyntax>())
        {
            token.ThrowIfCancellationRequested();
            var transformedProperty = TransformProperty(property, semanticModel, config, env, diagnostics);
            if (transformedProperty != null)
            {
                properties.Add(transformedProperty.Value);
            }
        }

        return new ClassInfo
        {
            Diagnostics = diagnostics,
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            Modifiers = classSyntax.Modifiers.Select(m => m.Text).ToArray(),
            Properties = properties,
            Location = LocationInfo.From(classSyntax),
        };
    }

    private static Dictionary<string, string>? LoadEnvironment(
        EnviedConfig config,
        SemanticModel semanticModel,
        List<DiagnosticInfo> diagnostics,
        Location location)
    {
        var projectRoot = Path.GetDirectoryName(semanticModel.SyntaxTree.FilePath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            diagnostics.Add(DiagnosticMessages.ProjectRootNotFound.WithLocation(location));
            return [];
        }

        var envPath = Path.Combine(projectRoot, config.Path);

        if (!File.Exists(envPath) && config.RequireEnvFile)
        {
            diagnostics.Add(DiagnosticMessages.MissingEnvFile.WithMessageArgs(config.Path).WithLocation(location));
            return [];
        }

        var env = DotEnv.Fluent().WithEnvFiles(envPath).WithTrimValues().Read();
        return env.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static PropertyInfo? TransformProperty(
        PropertyDeclarationSyntax property,
        SemanticModel semanticModel,
        EnviedConfig config,
        Dictionary<string, string> env,
        List<DiagnosticInfo> diagnostics)
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
        var fieldAttribute = property.AttributeLists.SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "EnviedField");

        if (fieldAttribute == null)
            return null;

        var location = LocationInfo.From(property);
        var fieldConfig = EnviedFieldConfig.From(
           fieldAttribute,
            config);

        var envName = fieldConfig.Name ?? fieldName;
        if (fieldConfig.UseConstantCase)
            envName = envName.ToUpper();

        var value = GetValue(
            envName,
            env,
            config.Environment,
            fieldConfig.DefaultValue,
            namedType,
            fieldConfig.Optional,
            property,
            diagnostics);

        if (string.IsNullOrEmpty(value))
        {
            return new PropertyInfo
            {
                Name = fieldName,
                Type = TypeInfo.From(namedType),
                Value = "null",
                Modifiers = modifiers,
                Location = location,
            };
        }


        if (property.Type is PredefinedTypeSyntax { Keyword.Text: "string" })
        {
            if (fieldConfig.Interpolate)
                value = InterpolateValue(value, fieldConfig, env, property, diagnostics);

            if (fieldConfig is { RawString: true, Obfuscate: false })
                value = EscapeString(value, fieldConfig.RawString);
        }

        var fieldValue = fieldConfig.Obfuscate
            ? ObfuscateField(value, fieldConfig.RandomSeed, KeyHelper.DeriveKey(semanticModel.Compilation.Assembly))
            : value;

        return new PropertyInfo
        {
            Name = fieldName,
            Type = TypeInfo.From(namedType),
            Value = TypeHelper.GetConversionExpression(fieldConfig.Obfuscate ? $"RuntimeKeyHelper.Decrypt(\"{fieldValue}\", _key)" : $"\"{fieldValue}\"", namedType),
            Modifiers = modifiers,
            IsObfuscated = fieldConfig.Obfuscate,
            Location = location,
        };
    }

    private static string? GetValue(
        string envName,
        Dictionary<string, string> env,
        bool environment,
        object? defaultValue,
        INamedTypeSymbol namedType,
        bool optional,
        PropertyDeclarationSyntax property,
        List<DiagnosticInfo> diagnostics)
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
                    _ => defaultValue.ToString() ?? string.Empty
                };
            }

            if (string.IsNullOrEmpty(value))
            {
                if (!optional)
                {
                    diagnostics.Add(DiagnosticMessages.MissingEnvironmentVariable.WithMessageArgs(envName).WithLocation(property.GetLocation()));
                    return null;
                }

                if (namedType.IsValueType && namedType.Name != "Nullable")
                {
                    diagnostics.Add(DiagnosticMessages.OptionalValueTypesMustBeNullable.WithLocation(property.GetLocation()).WithMessageArgs(property.Identifier.Text, namedType.Name));
                    return null;
                }
            }
        }

        if (environment && !string.IsNullOrEmpty(value))
        {
            var envValue = Environment.GetEnvironmentVariable(value);
            if (!string.IsNullOrEmpty(envValue))
                value = envValue;
        }

        if (string.IsNullOrEmpty(value) || TypeHelper.IsValidTypeConversion(value, namedType)) return value;

        diagnostics.Add(DiagnosticMessages.InvalidTypeConversion.WithMessageArgs(value, namedType.ToDisplayString()).WithLocation(property.GetLocation()));
        return null;
    }

    private static string InterpolateValue(string value, EnviedFieldConfig config, Dictionary<string, string> env, PropertyDeclarationSyntax property, List<DiagnosticInfo> diagnostics)
    {
        return InterpolationPattern.Replace(value, match =>
        {
            var envName = match.Groups[1].Value.Trim();
            if (config.UseConstantCase)
                envName = envName.ToUpper();

            if (env.TryGetValue(envName, out var replacement) || config.Optional) return replacement;

            diagnostics.Add(DiagnosticMessages.MissingEnvironmentVariable.WithMessageArgs(envName).WithLocation(property.GetLocation()));
            return match.Value;

        });
    }

    private static string EscapeString(string value, bool rawString)
    {
        if (!rawString)
            return EscapeSequenceRegex.Replace(value, match =>
            {
                if (match.Value.StartsWith("\\"))
                    return match.Value;
                return match.Value.Replace("\"", "\\\"");
            });


        int maxQuotes = QuoteRegex.Matches(value).Cast<Match>().Select(m => m.Length).DefaultIfEmpty(0).Max();
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
        var encrypted = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(value), 0, value.Length);

        Aes.Clear();
        return Convert.ToBase64String(iv.Concat(encrypted).ToArray());
    }

    public static bool IsOlderProject(AnalyzerConfigOptionsProvider analyzerConfig, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!analyzerConfig.GlobalOptions.TryGetValue("build_property.TargetFramework", out var targetFramework))
            return true;

        string versionString = targetFramework.Replace("net", "").Replace("coreapp", "");

        if (Version.TryParse(versionString, out var version))
            return !(version.Major >= 9);
        return true;
    }
}
