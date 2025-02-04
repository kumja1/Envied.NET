using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using dotenv.net;
using Envied.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Envied.SourceGenerator;

[Generator]

public class EnviedSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntaxProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Envied.EnviedAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax @class &&
                    @class.AttributeLists.Any(attrList =>
                        attrList.Attributes.Any(attr => attr.Name.ToString() == "Envied")),
                transform: static (context, _) => (Class: (ClassDeclarationSyntax)context.TargetNode, context.SemanticModel))
                .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(tuple => tuple.Left.Class != null);

        context.RegisterSourceOutput(syntaxProvider, (context, t) => GenerateClass(context, (t.Left.Class, t.Left.SemanticModel, t.Right)));
    }

    private void GenerateClass(SourceProductionContext context, (ClassDeclarationSyntax Class, SemanticModel SemanticModel, AnalyzerConfigOptionsProvider AnalyzerConfig) tuple)
    {
        var (@class, semanticModel, analyzerConfig) = tuple;

        if (!@class.Modifiers.Any(SyntaxKind.StaticKeyword) || !@class.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            ReportDiagnostic(context, "ENV004", "Class Must Be Partial and Static", $"The class '{@class.Identifier.Text}' must be declared partial and static.", @class.GetLocation());
            return;
        }

        var namespaceName = @class.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()?.Name.ToString();

        var attributeSyntax = @class.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name.ToString() == "Envied");

        if (attributeSyntax == null)
        {
            ReportDiagnostic(context, "ENV005", "Missing Envied Attribute", $"The class '{@class.Identifier.Text}' is missing the required [Envied] attribute.", @class.GetLocation());
            return;
        }

        var config = new EnviedAttribute(
            path: GetAttributeValue<string>(attributeSyntax, "path") ?? ".env",
            requireEnvFile: GetAttributeValue<bool>(attributeSyntax, "requireEnvFile"),
            name: GetAttributeValue<string>(attributeSyntax, "name")!,
            obfuscate: GetAttributeValue<bool>(attributeSyntax, "obfuscate"),
            allowOptionalFields: GetAttributeValue<bool>(attributeSyntax, "allowOptionalFields"),
            useConstantCase: GetAttributeValue<bool>(attributeSyntax, "useConstantCase"),
            randomSeed: GetAttributeValue<int>(attributeSyntax, "randomSeed")
        );

        var env = LoadEnvironment(config, analyzerConfig, context, @class.GetLocation());
        if (env == null && config.RequireEnvFile)
            return;

        var fieldsSource = GenerateFields(@class, config, semanticModel, env, context);
        var source = $@"
using Envied.SourceGenerator.Utils;
using System.Reflection;

namespace {namespaceName};

    public static partial class {@class.Identifier.Text}
    {{
        private static readonly byte[] Key = EnviedHelper.GetKey(Assembly.GetExecutingAssembly());
        {fieldsSource}
    }}";

        Log.FlushLogs(context);
        context.AddSource($"{@class.Identifier.Text}.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private static string GenerateFields(
        ClassDeclarationSyntax @class,
        EnviedAttribute config,
        SemanticModel semanticModel,
        IDictionary<string, string>? env,
        SourceProductionContext context)
    {
        var fieldsSource = new StringBuilder();
        var key = EnviedHelper.GetKey(semanticModel.Compilation.SourceModule.ContainingAssembly);

        foreach (var property in @class.Members.OfType<PropertyDeclarationSyntax>())
        {
            var enviedFieldAttribute = property.AttributeLists
                .SelectMany(attrList => attrList.Attributes)
                .FirstOrDefault(attr => attr.Name.ToString() == "EnviedField");

            if (enviedFieldAttribute == null)
                continue;

            var fieldName = property.Identifier.Text;
            var fieldType = property.Type;
            var envName = (GetAttributeValue<string>(enviedFieldAttribute, "name") ?? fieldName).Trim();

            if (GetAttributeValue<bool>(enviedFieldAttribute, "useConstantCase") || config.UseConstantCase)
                envName = envName.ToUpper();

            string value = string.Empty;
            if (env == null || !env.TryGetValue(envName, out value))
            {
                if (!(GetAttributeValue<bool>(enviedFieldAttribute, "optional") || (config.AllowOptionalFields && !fieldType.IsNotNull)))
                {
                    ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", property.GetLocation());
                    continue;
                }
            }

            if ((GetAttributeValue<bool>(enviedFieldAttribute, "interpolate") || config.Interpolate) &&
                fieldType is PredefinedTypeSyntax predefinedType &&
                predefinedType.Keyword.Text == "string")
            {
                value = InterpolationPattern.Replace(value, match =>
                {
                    var envName = match.Groups[1].Value.Trim();
                    if (config.UseConstantCase)
                        envName = envName.ToUpper();

                    var replacement = string.Empty;
                    if (env?.TryGetValue(envName, out replacement) != true)
                    {
                        if (!(GetAttributeValue<bool>(enviedFieldAttribute, "optional") || config.AllowOptionalFields))
                        {
                            ReportDiagnostic(context, "ENV002", "Missing Environment Variable", $"The environment variable '{envName}' is missing.", property.GetLocation());
                            return match.Value;
                        }
                    }

                    return replacement;
                });
            }

            INamedTypeSymbol namedType = (INamedTypeSymbol)semanticModel.GetTypeInfo(fieldType).Type!;
            bool isEnum = namedType != null && namedType.TypeKind == TypeKind.Enum;
            bool isEncrypted = GetAttributeValue<bool>(enviedFieldAttribute, "obfuscate") || config.Obfuscate;

            var encryptedValue = isEncrypted ? EncryptField(value, config.RandomSeed == 0 ? GetAttributeValue<int>(enviedFieldAttribute, "randomSeed") : config.RandomSeed, key) : value;

            if (isEnum && !namedType!.GetMembers().Any(m => m.Name == value))
            {
                ReportDiagnostic(context, "ENV003", "Invalid Enum Value", $"The value '{value}' is not a valid enum value for '{fieldName}'.", property.GetLocation());
                continue;
            }

            var privateField = fieldName.ToLower();
            fieldsSource.AppendLine(@$"public static partial {fieldType} {fieldName} {{ get => _{privateField}; }}");
            fieldsSource.AppendLine(@$"private static readonly {fieldType} _{privateField} = ({namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){EnviedHelper.GetTypeConversionKeyword(isEncrypted ? $"EnviedHelper.Decrypt(\"{encryptedValue}\", Key)" : encryptedValue, namedType)};");
        }

        return fieldsSource.ToString();
    }

    private static string EncryptField(string value, int seed, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;

        var random = new Random((int)(seed == 0 ? DateTime.Now.Ticks % int.MaxValue : seed));
        var iv = new byte[16];
        random.NextBytes(iv);
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var encrypted = encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(value), 0, value.Length);

        return Convert.ToBase64String([.. iv, .. encrypted]);
    }

    private static IDictionary<string, string>? LoadEnvironment(
        EnviedAttribute config, AnalyzerConfigOptionsProvider analyzerConfig, SourceProductionContext context, Location location)
    {
        try
        {
            if (!analyzerConfig.GlobalOptions.TryGetValue("build_property.projectdir", out var projectRoot) ||
          string.IsNullOrEmpty(projectRoot))
            {
                ReportDiagnostic(context, "ENV006", "Project Root Not Found", "Could not determine project root directory", location);
                return null;
            }

            var envPath = Path.Combine(projectRoot, config.Path);

            return DotEnv
                .Fluent()
                .WithEnvFiles(envPath)
                .WithTrimValues()
                .WithExceptions()
                .Read();
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

    private static T? GetAttributeValue<T>(AttributeSyntax attribute, string name)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => string.Equals((arg.NameColon?.Name ?? arg.NameEquals?.Name)!.Identifier.Text, name, StringComparison.OrdinalIgnoreCase));

        if (argument?.Expression is LiteralExpressionSyntax literal)
            return (T)Convert.ChangeType(literal.Token.Value, typeof(T));
        return default;
    }

    private static void ReportDiagnostic(SourceProductionContext context, string id, string title, string message, Location location)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(
                id,
                title,
                message,
                "Usage",
                DiagnosticSeverity.Error,
                true),
            location));
    }

    private static readonly Regex InterpolationPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
}
