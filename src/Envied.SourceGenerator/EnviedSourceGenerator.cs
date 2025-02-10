using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using dotenv.net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Envied.SourceGenerator.Utils;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;
using Envied.SourceGenerator.Models;

namespace Envied.SourceGenerator;

[Generator]
public class EnviedSourceGenerator : IIncrementalGenerator
{
    private static readonly Regex InterpolationPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex QuoteRegex = new(@"""+", RegexOptions.Compiled);
    private static readonly Regex EscapeSequenceRegex = new(
        @"(?<!\\)(\\\\)*(\\[\\""abfnrtv]|\\u[0-9a-fA-F]{4}|\\U[0-9a-fA-F]{8})",
        RegexOptions.Compiled
    );

    private static readonly Aes Aes = Aes.Create();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analyzerConfigOptionsProvider = context.AnalyzerConfigOptionsProvider.Select((config, _) => (IsOlderVersion: IsOlderFramework(config), ProjectRoot: config.GlobalOptions.TryGetValue("build_property.projectdir", out var projectRoot) ? projectRoot : string.Empty));
        var compilationProvider = context.CompilationProvider.Select((compilation, _) => compilation.Assembly);

        var syntaxProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName("Envied.EnviedAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (context, _) => ClassInfo.From((ClassDeclarationSyntax)context.TargetNode, (INamedTypeSymbol)context.TargetSymbol))
            .Combine(analyzerConfigOptionsProvider)
            .Combine(compilationProvider)
            .Select((tuple, _) => (Class: tuple.Left.Left, IsOlder: tuple.Left.Right, AssemblyKey: new ValueEquatableArray<byte>(GeneratorHelper.DeriveKey(tuple.Right))));

        context.RegisterSourceOutput(syntaxProvider, GenerateClass);
    }

    private void GenerateClass(SourceProductionContext context, (ClassInfo Class, bool IsOlder, ValueEquatableArray<byte> AssemblyKey) tuple)
    {
        var (@class, isOlder, key) = tuple;

        if (!@class.Modifiers.Contains("static") || (!isOlder && !@class.Modifiers.Contains("partial")))
        {
            string partial = isOlder ? string.Empty : " Partial";
            ReportDiagnostic(context, "ENV004", $"Class Must Be{partial} and Static", $"The class '{@class.Name}' must be declared{partial.ToLower()} and static.", @class.GetLocation());
            return;
        }

        var attribute = @class.Attributes.FirstOrDefault(attr => attr.Name == "EnviedAttribute");

        var config = new EnviedConfig(
            Path: GetAttributeArgument<string>(attribute, "path") ?? ".env",
            RequireEnvFile: GetAttributeArgument<bool>(attribute, "requireEnvFile"),
            Name: GetAttributeArgument<string>(attribute, "name"),
            Obfuscate: GetAttributeArgument<bool>(attribute, "obfuscate"),
            AllowOptionalFields: GetAttributeArgument<bool>(attribute, "allowOptionalFields"),
            UseConstantCase: GetAttributeArgument<bool>(attribute, "useConstantCase"),
            Interpolate: GetAttributeArgument<bool>(attribute, "interpolate"),
            RawStrings: GetAttributeArgument<bool>(attribute, "rawStrings"),
            Environment: GetAttributeArgument<bool>(attribute, "environment"),
            RandomSeed: GetAttributeArgument<int>(attribute, "randomSeed")
        );

        var env = LoadEnvironment(config, analyzerConfig, context, @class.GetLocation());
        if (env == null && config.RequireEnvFile)
            return;

        var fieldsSource = GenerateFields(@class, isOlder, config, env, context);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace {@class.Namespace};  
 
"""
);
        sb.AppendLine(isOlder ? @$"public static class {@class.Name}_Generated " : @$"public static partial class {@class.Name} ");
        sb.AppendLine(@$"{{
            {fieldsSource}
            }}");

        context.AddSource($"{@class.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static bool IsOlderFramework(AnalyzerConfigOptionsProvider analyzerConfig)
    {
        if (analyzerConfig.GlobalOptions.TryGetValue("build_property.TargetFramework", out var targetFramework))
        {
            if (targetFramework.StartsWith("netstandard") && Version.TryParse(targetFramework.Replace("netstandard", ""), out var parsedVersion))
                return parsedVersion < new Version(2, 1);
            else if ((targetFramework.StartsWith("net") || targetFramework.StartsWith("netcoreapp")) && Version.TryParse(targetFramework.Replace("net", "").Replace("coreapp", ""), out var netParsedVersion))
                return netParsedVersion < new Version(9, 0);
        }
        return true;
    }

    private static string GenerateFields(
        ClassInfo @class,
        bool isOlder,
        EnviedConfig config,
        IDictionary<string, string> env,
        SourceProductionContext context)
    {
        var fieldsSource = new StringBuilder();
        int obfuscatedFields = 0;

        foreach (var member in @class.Members)
        {
            var fieldSource = GenerateField(member, isOlder, config, env, context, key, ref obfuscatedFields);

            if (string.IsNullOrEmpty(fieldSource)) continue;

            fieldsSource.AppendLine(fieldSource);
        }

        if (obfuscatedFields > 0)
            fieldsSource.Insert(0, $"private static readonly byte[] _key = EnviedHelper.DeriveKey(Assembly.GetCallingAssembly());\n");

        return fieldsSource.ToString();
    }

    private static string GenerateField(
        FieldInfo field,
        bool isOlder,
        EnviedConfig config,
        IDictionary<string, string> env,
        SourceProductionContext context,
        byte[] key,
        ref int obfuscateFields)
    {
        if (!field.Modifiers.Contains("static") || (!isOlder && !field.Modifiers.Contains("partial")))
        {
            ReportDiagnostic(context, "ENV007", "Field Must Be Static", $"The field '{field.Name}' must be declared static. Skipping {field.Name}", field.GetLocation(), DiagnosticSeverity.Warning);
            return string.Empty;
        }

        var attribute = field.Attributes.FirstOrDefault(attr => attr.Name == "EnviedField");

        if (attribute == null)
            return string.Empty;

        var (envName, useConstantCase, optional, interpolate, rawString, obfuscate, environment, defaultValue, randomSeed) = GetFieldAttributeArguments(attribute);

        envName ??= field.Name;
        optional = optional || config.AllowOptionalFields;
        interpolate = interpolate || config.Interpolate;
        rawString = rawString || config.RawStrings;
        obfuscate = obfuscate || config.Obfuscate;
        environment = environment || config.Environment;
        randomSeed = randomSeed != 0 ? randomSeed : config.RandomSeed;
        useConstantCase = useConstantCase || config.UseConstantCase;

        if (useConstantCase)
            envName = envName.ToUpper();

        string value = GetValue(envName, env, environment, defaultValue, field.Type, optional, context, field);
        if (!optional && value == null)
            return string.Empty;

        bool isEmptyOrNull = string.IsNullOrEmpty(value);
        if (field.Type == "string" && !isEmptyOrNull)
        {
            if (interpolate)
                value = InterpolateValue(value, config, env, context, field);

            if (rawString && !obfuscate)
                value = EscapeString(value, rawString);
        }

        var fieldValue = obfuscate ? ObfuscateField(value, randomSeed, key) : value;
        if (obfuscate) obfuscateFields++;

        var privateField = field.Name.ToLower();
        return $"\npublic static {(isOlder ? string.Empty : "partial ")}{field.Type} {field.Name} => _{privateField};\n\nprivate static readonly {field.Type} _{privateField} = {GeneratorHelper.GetTypeConversionFor(obfuscate ? $"EnviedHelper.Decrypt(\"{fieldValue}\", _key)" : isEmptyOrNull ? "null" : $"\"{fieldValue}\"", field.Type)};";
    }

    private static string GetValue(string envName, IDictionary<string, string> env, bool environment, object? defaultValue, TypeInfo fieldType, bool optional, SourceProductionContext context, FieldInfo field)
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
                    ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", field.GetLocation(), DiagnosticSeverity.Error);
                    return null;
                }

                if (fieldType.Name != "string" && !field.IsNullable)
                {
                    ReportDiagnostic(context, "ENV008", "Non-string optional fields must be nullable", $"Optional field '{field.Name}' of type {fieldType} must be nullable", field.GetLocation());
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

        if (!string.IsNullOrEmpty(value) && !GeneratorHelper.IsValidTypeConversion(value, fieldType))
        {
            ReportDiagnostic(context, "ENV005", "Invalid Type Conversion", $"Cannot convert '{value}' to type '{fieldType}'.", field.GetLocation());
            return null;
        }

        return value;
    }

    private static string InterpolateValue(string value, EnviedConfig config, IDictionary<string, string> env, SourceProductionContext context, FieldInfo field)
    {
        return InterpolationPattern.Replace(value, match =>
        {
            var envName = match.Groups[1].Value.Trim();
            if (config.UseConstantCase)
                envName = envName.ToUpper();

            if (!env.TryGetValue(envName, out var replacement))
            {
                if (!config.AllowOptionalFields)
                {
                    ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", field.GetLocation());
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
        return Convert.ToBase64String(iv.Concat(encrypted).ToArray());
    }

    private static IDictionary<string, string> LoadEnvironment(
        EnviedConfig config,
        AnalyzerConfigOptionsProvider analyzerConfig,
        SourceProductionContext context,
        Location location)
    {
        if (!File.Exists(config.Path)) ReportDiagnostic(context, "ENV001", "Missing Environment File", $"The environment file '{config.Path}' is missing.", location);


        if (!analyzerConfig.GlobalOptions.TryGetValue("build_property.projectdir", out var projectRoot) || string.IsNullOrEmpty(projectRoot))
        {
            ReportDiagnostic(context, "ENV006", "Project Root Not Found", "Could not determine project root directory", location);
            return null;
        }

        var envPath = Path.Combine(projectRoot, config.Path);
        return DotEnv.Fluent().WithEnvFiles(envPath).WithTrimValues().Read();
    }

    private static (string? Name, bool UseConstantCase, bool Optional, bool Interpolate, bool RawString, bool Obfuscate, bool Environment, object? DefaultValue, int RandomSeed) GetFieldAttributeArguments(AttributeInfo attribute)
    {
        return (
            Name: GetAttributeArgument<string>(attribute, "name"),
            UseConstantCase: GetAttributeArgument<bool>(attribute, "useConstantCase"),
            Optional: GetAttributeArgument<bool>(attribute, "optional"),
            Interpolate: GetAttributeArgument<bool>(attribute, "interpolate"),
            RawString: GetAttributeArgument<bool>(attribute, "rawString"),
            Obfuscate: GetAttributeArgument<bool>(attribute, "obfuscate"),
            Environment: GetAttributeArgument<bool>(attribute, "environment"),
            DefaultValue: GetAttributeArgument<object>(attribute, "defaultValue"),
            RandomSeed: GetAttributeArgument<int>(attribute, "randomSeed")
        );
    }

    private static T? GetAttributeArgument<T>(AttributeInfo attribute, string name)
    {
        var argument = attribute.NamedArguments
            .FirstOrDefault(arg => string.Equals(arg.Name, name, StringComparison.OrdinalIgnoreCase));

        if (argument.Value is T value)
            return value;
        return default;
    }

    private static void ReportDiagnostic(SourceProductionContext context, string id, string title, string message, Location location, DiagnosticSeverity severity = DiagnosticSeverity.Error)
    {
        var diagnostic = Diagnostic.Create(new DiagnosticDescriptor(id, title, message, "Envied", severity, true), location);
        context.ReportDiagnostic(diagnostic);
    }
}
