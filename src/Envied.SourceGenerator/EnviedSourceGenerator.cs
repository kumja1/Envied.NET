using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using dotenv.net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Envied.SourceGenerator.Utils;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

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
        var syntaxProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Envied.EnviedAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (context, _) => (Class: (ClassDeclarationSyntax)context.TargetNode, context.SemanticModel))
                .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(tuple => tuple.Left.Class != null);

        context.RegisterSourceOutput(syntaxProvider, (context, t) => GenerateClass(context, (t.Left.Class, t.Left.SemanticModel, t.Right)));
    }

    private void GenerateClass(SourceProductionContext context, (ClassDeclarationSyntax Class, SemanticModel SemanticModel, AnalyzerConfigOptionsProvider AnalyzerConfig) tuple)
    {
        var (@class, semanticModel, analyzerConfig) = tuple;

        bool isOlder = IsOlderFramework(analyzerConfig);
        if (!@class.Modifiers.Any(SyntaxKind.StaticKeyword) || (!isOlder && !@class.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            string partial = isOlder ? string.Empty : " Partial";
            ReportDiagnostic(context, "ENV004", $"Class Must Be{partial} and Static", $"The class '{@class.Identifier.Text}' must be declared{partial.ToLower()} and static.", @class.GetLocation());
            return;
        }

        var namespaceName = @class.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        var attributeSyntax = @class.AttributeLists.SelectMany(al => al.Attributes).FirstOrDefault(attr => attr.Name.ToString() == "Envied");

        var config = new EnviedConfig(
            path: GetAttributeArgument<string>(attributeSyntax, "path") ?? ".env",
            requireEnvFile: GetAttributeArgument<bool>(attributeSyntax, "requireEnvFile"),
            name: GetAttributeArgument<string>(attributeSyntax, "name"),
            obfuscate: GetAttributeArgument<bool>(attributeSyntax, "obfuscate"),
            allowOptionalFields: GetAttributeArgument<bool>(attributeSyntax, "allowOptionalFields"),
            useConstantCase: GetAttributeArgument<bool>(attributeSyntax, "useConstantCase"),
            interpolate: GetAttributeArgument<bool>(attributeSyntax, "interpolate"),
            rawStrings: GetAttributeArgument<bool>(attributeSyntax, "rawStrings"),
            environment: GetAttributeArgument<bool>(attributeSyntax, "environment"),
            randomSeed: GetAttributeArgument<int>(attributeSyntax, "randomSeed")
        );

        var env = LoadEnvironment(config, analyzerConfig, context, @class.GetLocation());
        if (env == null && config.RequireEnvFile)
            return;

        var fieldsSource = GenerateFields(@class, isOlder, config, semanticModel, env, context);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace {namespaceName};  
 
"""
);
        sb.AppendLine(isOlder ? @$"public static class {@class.Identifier.Text}_Generated " : @$"public static partial class {@class.Identifier.Text} ");
        sb.AppendLine(@$"{{
            {fieldsSource}
            }}");
        
       // DebugLog.Flush(context);
        context.AddSource($"{@class.Identifier.Text}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static bool IsOlderFramework(AnalyzerConfigOptionsProvider analyzerConfig)
    {
        if (analyzerConfig.GlobalOptions.TryGetValue("build_property.TargetFramework", out var targetFramework))
        {
            if (targetFramework.StartsWith("netstandard") && Version.TryParse(targetFramework.Replace("netstandard", ""), out var parsedVersion))
            {
                return parsedVersion < new Version(2, 1);
            }
            else if ((targetFramework.StartsWith("net") || targetFramework.StartsWith("netcoreapp")) && Version.TryParse(targetFramework.Replace("net", "").Replace("coreapp", ""), out var netParsedVersion))
            {
                return netParsedVersion < new Version(9, 0);
            }
        }
        return true;
    }

    private static string GenerateFields(
        ClassDeclarationSyntax @class,
        bool isOlder,
        EnviedConfig config,
        SemanticModel semanticModel,
        IDictionary<string, string> env,
        SourceProductionContext context)
    {
        var fieldsSource = new StringBuilder();
        var key = GeneratorHelper.DeriveKey(semanticModel.Compilation.Assembly);
        int obfuscatedFields = 0;

        foreach (var member in @class.Members)
        {
            if (member is not PropertyDeclarationSyntax property) continue;
            var fieldSource = GenerateField(property, isOlder, config, semanticModel, env, context, key, ref obfuscatedFields);

            if (string.IsNullOrEmpty(fieldSource)) continue;
            fieldsSource.AppendLine(fieldSource);
        }

        if (obfuscatedFields > 0)
            fieldsSource.Insert(0, $"private static readonly byte[] _key = EnviedHelper.DeriveKey(Assembly.GetCallingAssembly());\n");

        return fieldsSource.ToString();
    }

    private static string GenerateField(
        PropertyDeclarationSyntax property,
        bool isOlder,
        EnviedConfig config,
        SemanticModel semanticModel,
        IDictionary<string, string> env,
        SourceProductionContext context,
        byte[] key,
        ref int obfuscateFields)
    {
        if (!property.Modifiers.Any(SyntaxKind.StaticKeyword) || (!isOlder && !property.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            ReportDiagnostic(context, "ENV007", "Field Must Be Static", $"The field '{property.Identifier.Text}' must be declared static. Skipping {property.Identifier.Text}", property.GetLocation(), DiagnosticSeverity.Warning);
            return string.Empty;
        }

        AttributeSyntax enviedFieldAttribute = property.AttributeLists
             .SelectMany(attrList => attrList.Attributes)
             .FirstOrDefault(attr => attr.Name.ToString() == "EnviedField");

        if (enviedFieldAttribute == null)
            return string.Empty;

        string fieldName = property.Identifier.Text;
        TypeSyntax fieldType = property.Type;
        TypeInfo typeInfo = semanticModel.GetTypeInfo(fieldType);
        INamedTypeSymbol namedType = (INamedTypeSymbol)typeInfo.Type!;

        var (envName, useConstantCase, optional, interpolate, rawString, obfuscate, environment, defaultValue, randomSeed) = GetFieldAttributeArguments(enviedFieldAttribute);

        envName ??= fieldName;
        optional = optional || config.AllowOptionalFields;
        interpolate = interpolate || config.Interpolate;
        rawString = rawString || config.RawStrings;
        obfuscate = obfuscate || config.Obfuscate;
        environment = environment || config.Environment;
        randomSeed = randomSeed != 0 ? randomSeed : config.RandomSeed;
        useConstantCase = useConstantCase || config.UseConstantCase;

        if (useConstantCase)
            envName = envName.ToUpper();

        string value = GetValue(envName, env, environment, defaultValue, namedType, optional, context, property);
       // DebugLog.WriteLine($"{envName}:{value}");
        if (!optional  && value == null)
            return string.Empty;

        bool isEmptyOrNull = string.IsNullOrEmpty(value);
        if (fieldType is PredefinedTypeSyntax predefinedType && predefinedType.Keyword.Text == "string" && !isEmptyOrNull)
        {
            if (interpolate)
                value = InterpolateValue(value, config, env, context, property);

            if (rawString && !obfuscate)
                value = EscapeString(value, rawString);
        }

        var fieldValue = obfuscate ? ObfuscateField(value, randomSeed, key) : value;
        if (obfuscate) obfuscateFields++;

        var privateField = fieldName.ToLower();
        return $"\npublic static {(isOlder ? string.Empty : "partial ")}{fieldType} {fieldName} => _{privateField};\n\nprivate static readonly {fieldType} _{privateField} = {GeneratorHelper.GetTypeConversionFor(obfuscate ? $"EnviedHelper.Decrypt(\"{fieldValue}\", _key)" : isEmptyOrNull ? "null" : $"\"{fieldValue}\"", namedType)};";
    }

    private static string GetValue(string envName, IDictionary<string, string> env, bool environment, object? defaultValue, INamedTypeSymbol namedType, bool optional, SourceProductionContext context, PropertyDeclarationSyntax property)
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
                    ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", property.GetLocation(), DiagnosticSeverity.Error);
                    return null;
                }

                if (namedType.IsValueType && namedType.Name != "Nullable")
                {
                    ReportDiagnostic(context, "ENV008", "Non-string optional fields must be nullable", $"Optional field '{property.Identifier.Text}' of type {namedType} must be nullable", property.GetLocation());
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

        if (!string.IsNullOrEmpty(value) && !GeneratorHelper.IsValidTypeConversion(value, namedType))
        {
            ReportDiagnostic(context, "ENV005", "Invalid Type Conversion", $"Cannot convert '{value}' to type '{namedType.ToDisplayString()}'.", property.GetLocation());
            return null;
        }

        return value;
    }

    private static string InterpolateValue(string value, EnviedConfig config, IDictionary<string, string> env, SourceProductionContext context, PropertyDeclarationSyntax property)
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
                    ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", property.GetLocation());
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

    private static IDictionary<string, string> LoadEnvironment(
        EnviedConfig config,
        AnalyzerConfigOptionsProvider analyzerConfig,
        SourceProductionContext context,
        Location location)
    {
        try
        {
            if (!analyzerConfig.GlobalOptions.TryGetValue("build_property.projectdir", out var projectRoot) || string.IsNullOrEmpty(projectRoot))
            {
                ReportDiagnostic(context, "ENV006", "Project Root Not Found", "Could not determine project root directory", location);
                return null;
            }

            var envPath = Path.Combine(projectRoot, config.Path);
            return DotEnv.Fluent().WithEnvFiles(envPath).WithTrimValues().WithExceptions().Read();
        }
        catch (FileNotFoundException)
        {
            if (config.RequireEnvFile)
            {
                ReportDiagnostic(context, "ENV001", "Missing Environment File", $"The environment file '{config.Path}' is missing.", location);
            }
            return null;
        }
    }

    private static (string? Name, bool UseConstantCase, bool Optional, bool Interpolate, bool RawString, bool Obfuscate, bool Environment, object? DefaultValue, int RandomSeed) GetFieldAttributeArguments(AttributeSyntax attribute)
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

    private static T? GetAttributeArgument<T>(AttributeSyntax attribute, string name)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => string.Equals((arg.NameColon?.Name ?? arg.NameEquals?.Name)!.Identifier.Text, name, StringComparison.OrdinalIgnoreCase));

        if (argument?.Expression is LiteralExpressionSyntax literal)
            return (T)Convert.ChangeType(literal.Token.Value, typeof(T));
        return default;
    }

    private static void ReportDiagnostic(SourceProductionContext context, string id, string title, string message, Location location, DiagnosticSeverity severity = DiagnosticSeverity.Error) => context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id,
                title,
                message,
                "Usage",
                severity,
                true),
            location));
}

public readonly record struct EnviedConfig
{
    public string Path { get; init; }
    public bool RequireEnvFile { get; init; }
    public string Name { get; init; }
    public bool Obfuscate { get; init; }
    public bool AllowOptionalFields { get; init; }
    public bool UseConstantCase { get; init; }
    public bool Interpolate { get; init; }
    public bool RawStrings { get; init; }
    public bool Environment { get; init; }
    public int RandomSeed { get; init; }

    public EnviedConfig(
        string path,
        bool requireEnvFile,
        string name,
        bool obfuscate,
        bool allowOptionalFields,
        bool useConstantCase,
        bool interpolate,
        bool rawStrings,
        bool environment,
        int randomSeed)
    {
        Path = path;
        RequireEnvFile = requireEnvFile;
        Name = name;
        Obfuscate = obfuscate;
        AllowOptionalFields = allowOptionalFields;
        UseConstantCase = useConstantCase;
        Interpolate = interpolate;
        RawStrings = rawStrings;
        Environment = environment;
        RandomSeed = randomSeed;
    }
}
