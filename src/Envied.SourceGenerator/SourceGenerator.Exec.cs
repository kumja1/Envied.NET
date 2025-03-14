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

public static class Exec
{

    private static readonly Regex InterpolationPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    private static readonly Regex QuoteRegex = new(@"""+", RegexOptions.Compiled);
    private static readonly Regex EscapeSequenceRegex = new(
        @"(?<!\\)(\\\\)*(\\[\\""abfnrtv]|\\u[0-9a-fA-F]{4}|\\U[0-9a-fA-F]{8})",
        RegexOptions.Compiled
    );

    private static readonly Aes Aes = Aes.Create();

    public static ClassInfo TransformClass(
        ClassDeclarationSyntax classSyntax,
        ISymbol classSymbol,
        SemanticModel semanticModel,
        AnalyzerConfigOptionsProvider analyzerConfig)
    {
        var diagnostics = new List<DiagnosticInfo>();

        bool isOlder = IsOlderProject(analyzerConfig);
        if (!classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword) || (!isOlder && !classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            diagnostics.Add(new DiagnosticInfo(
                 "ENV003",
                 "Class Must Be Partial and Static",
                 $"The class '{classSyntax.Identifier.Text}' must be declared partial and static.",
                 DiagnosticSeverity.Error,
                 classSyntax.GetLocation()));
            return new ClassInfo
            {
                Name = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                Properties = [],
                Diagnostics = diagnostics,
                UsePartial = isOlder,
            };
        }

        var attributeSyntax = classSyntax.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "Envied");

        var config = EnviedConfig.From(attributeSyntax);
        var env = LoadEnvironment(config, analyzerConfig, diagnostics, classSyntax.GetLocation());

        var properties = new List<PropertyInfo>();
        foreach (var member in classSyntax.Members)
        {
            if (member is not PropertyDeclarationSyntax property)
                continue;

            var transformedProperty = TransformProperty(property, semanticModel, config, env, diagnostics);
            if (transformedProperty != null)
                properties.Add(transformedProperty.Value);
        }

        return new ClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            Properties = properties,
            Diagnostics = diagnostics,
            UsePartial = IsOlderProject(analyzerConfig),
        };
    }

    private static Dictionary<string, string> LoadEnvironment(
        EnviedConfig config,
        AnalyzerConfigOptionsProvider analyzerConfig,
        List<DiagnosticInfo> diagnostics,
        Location location)
    {
        if (!File.Exists(config.Path))
        {
            diagnostics.Add(new DiagnosticInfo(
                "ENV001",
                "Missing Environment File",
                $"The environment file '{config.Path}' is missing.",
                DiagnosticSeverity.Error,
                location));
            return [];
        }

        if (!analyzerConfig.GlobalOptions.TryGetValue("build_property.projectdir", out var projectRoot) ||
            string.IsNullOrEmpty(projectRoot))
        {
            diagnostics.Add(new DiagnosticInfo(
                "ENV006",
                "Project Root Not Found",
                "Could not determine project root directory",
                DiagnosticSeverity.Error,
                location));
            return [];
        }

        var envPath = Path.Combine(projectRoot, config.Path);
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

        var propertySymbol = semanticModel.GetDeclaredSymbol(property)!;
        var modifiers = property.Modifiers.Select(m => m.Text).ToArray();
        var typeInfo = semanticModel.GetTypeInfo(property.Type);
        var namedType = (INamedTypeSymbol)typeInfo.Type!;
        var fieldName = property.Identifier.Text;
        var fieldConfig = EnviedFieldConfig.From(
            property.AttributeLists.SelectMany(al => al.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() == "EnviedField"),
            config);

        var envName = fieldConfig.Name ?? fieldName;
        if (config.UseConstantCase)
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

        if (value == null)
        {
            return new PropertyInfo
            {
                Name = fieldName,
                Type = TypeInfo.From(namedType),
                Value = "null",
                Modifiers = modifiers ,
            };
        }

        bool isEmptyOrNull = string.IsNullOrEmpty(value);
        if (property.Type is PredefinedTypeSyntax predefinedType &&
            predefinedType.Keyword.Text == "string" &&
            !isEmptyOrNull)
        {
            if (fieldConfig.Interpolate)
                value = InterpolateValue(value, fieldConfig, env, property, diagnostics);

            if (fieldConfig.RawString && !fieldConfig.Obfuscate)
                value = EscapeString(value, fieldConfig.RawString);
        }

        var fieldValue = fieldConfig.Obfuscate
            ? ObfuscateField(value, fieldConfig.RandomSeed, KeyHelper.DeriveKey(semanticModel.Compilation.Assembly))
            : value;


        return new PropertyInfo
        {
            Name = fieldName,
            Type = TypeInfo.From(namedType),
            Value = TypeHelper.GetConversionExpression(fieldValue ?? "null", namedType),
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
        bool optional,
        PropertyDeclarationSyntax property,
        List<DiagnosticInfo> diagnostics)
    {
        if (!env.TryGetValue(envName, out string value))
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
                    diagnostics.Add(new DiagnosticInfo(
                        "ENV002",
                        "Missing Environment Variable",
                        $"The environment variable '{envName}' is missing.",
                        DiagnosticSeverity.Error,
                        property.GetLocation()));
                    return null;
                }

                if (namedType.IsValueType && namedType.Name != "Nullable")
                {
                    diagnostics.Add(new DiagnosticInfo(
                        "ENV008",
                        "Non-string optional fields must be nullable",
                        $"Optional field '{property.Identifier.Text}' of type {namedType} must be nullable",
                        DiagnosticSeverity.Error,
                        property.GetLocation()));
                    return null;
                }
            }
        }

        if (environment)
        {
            var envValue = Environment.GetEnvironmentVariable(value);
            if (!string.IsNullOrEmpty(envValue))
                value = envValue;
        }

        if (!string.IsNullOrEmpty(value) && !TypeHelper.IsValidTypeConversion(value, namedType))
        {
            diagnostics.Add(new DiagnosticInfo(
                "ENV005",
                "Invalid Type Conversion",
                $"Cannot convert '{value}' to type '{namedType.ToDisplayString()}'.",
                DiagnosticSeverity.Error,
                property.GetLocation()));
            return null;
        }

        return value;
    }

    private static string InterpolateValue(string value, EnviedFieldConfig config, Dictionary<string, string> env, PropertyDeclarationSyntax property, List<DiagnosticInfo> diagnostics)
    {
        return InterpolationPattern.Replace(value, match =>
        {
            var envName = match.Groups[1].Value.Trim();
            if (config.UseConstantCase)
                envName = envName.ToUpper();

            if (!env.TryGetValue(envName, out var replacement))
            {
                if (!config.Optional)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        "ENV002",
                        "Missing Environment Variable",
                        $"The environment variable '{envName}' is missing.",
                        DiagnosticSeverity.Error,
                        property.GetLocation()));
                    return match.Value;
                }
            }

            return replacement;
        });
    }


    private static string EscapeString(string value, bool rawString)
    {
        if (rawString)
        {
            var maxQuotes = QuoteRegex.Matches(value).Cast<Match>().Select(m => m.Length).DefaultIfEmpty(0).Max();
            int delimiterQuotes = Math.Max(3, maxQuotes + 1);
            string delimiter = new('"', delimiterQuotes);
            string escapedValue = value.Replace(delimiter, $"{delimiter}\"");

            return $$"""
            {{delimiter}}
            {{escapedValue}}
            {{delimiter}}
            """;
        }
        else
        {
            return EscapeSequenceRegex.Replace(value, match =>
            {
                if (match.Value.StartsWith("\\"))
                    return match.Value;
                return match.Value.Replace("\"", "\\\"");
            });
        }
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
        return Convert.ToBase64String([.. iv, .. encrypted]);
    }


    private static bool IsOlderProject(AnalyzerConfigOptionsProvider analyzerConfig)
    {
        if (analyzerConfig.GlobalOptions.TryGetValue("build_property.TargetFramework", out var targetFramework))
        {
            string versionString = targetFramework.Replace("net", "").Replace("coreapp", "");

            if (Version.TryParse(versionString, out var version))
            {
                return version.Major >= 9;
            }
        }
        return true;
    }
}
