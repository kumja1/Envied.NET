
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using dotenv.net;
using System.Security.Cryptography;

namespace Envied.Generators;

class EnviedSourceGenerator : ISourceGenerator
{
    public List<string> Sources { get; set; } = [];
    public void Execute(GeneratorExecutionContext context)
    {

        context.Compilation.SyntaxTrees
            .SelectMany(syntaxTree =>
            {
                Sources.Add(syntaxTree.FilePath);
                return syntaxTree.GetRoot().DescendantNodes();
            })
            .OfType<ClassDeclarationSyntax>()
            .Where(classDeclaration => classDeclaration.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Any(attribute => attribute.Name.ToString() == "Envied"))
            .ToList().ForEach(@class => GenerateClass(@class, context));
    }



    public void Initialize(GeneratorInitializationContext context)
    {
    }

    private void GenerateClass(ClassDeclarationSyntax @class, GeneratorExecutionContext context)
    {
        var source = new StringBuilder();
        source.AppendLine("using Envied.Utils;");

        var attribute = @class.AttributeLists
                            .SelectMany(attributeList => attributeList.Attributes)
                            .First(attribute => attribute.Name.ToString() == "Envied");
        var config = new Envied(
            path: GetAttributeArgumentValue<string>(attribute, "path"),
            interpolate: GetAttributeArgumentValue<bool>(attribute, "interpolate"),
            requireEnvFile: GetAttributeArgumentValue<bool>(attribute, "requireEnvFile"),
            name: GetAttributeArgumentValue<string>(attribute, "name"),
            obfuscate: GetAttributeArgumentValue<bool>(attribute, "obfuscate"),
            allowOptionalFields: GetAttributeArgumentValue<bool>(attribute, "allowOptionalFields"),
            useConstantCase: GetAttributeArgumentValue<bool>(attribute, "useConstantCase"),
            environment: GetAttributeArgumentValue<bool>(attribute, "environment"),
            rawStrings: GetAttributeArgumentValue<bool>(attribute, "rawStrings"),
            randomSeed: GetAttributeArgumentValue<int?>(attribute, "randomSeed")
        );

        if (string.IsNullOrEmpty(config.Name))
        {
            config.Name = @class.Identifier.Text;
        }


        var enviedFields = @class.Members.Select(member => member as FieldDeclarationSyntax)
            .Where(fieldDeclaration => fieldDeclaration.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Any(attribute => attribute.Name.ToString() == "EnviedField"))
            .ToList();

        var env = DotEnv.Fluent()
            .WithEnvFiles(config.Path)
            .WithTrimValues()
            .WithProbeForEnv()
            .WithDefaultEncoding()
            .Read();

        if (env == null && config.RequireEnvFile)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "ENV001",
                    "Missing Environment File",
                    $"The environment file {config.Path} is missing.",
                    "Usage",
                    DiagnosticSeverity.Warning,
                    true),
                @class.GetLocation()));
            return;
        }

        source.AppendLine($"class _{config.Name} {{");
        source.AppendLine("private static readonly byte[] Key = GetKey();");
        source.AppendLine(GenerateFields(enviedFields, config, env, context));

        source.AppendLine(""" 
        private static byte[] GetKey()
        {
            using SHA256 sha256 = SHA256.Create();
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var source in assembly.GetFiles())
            {
                var bytes = new byte[];
                source.Read(bytes);
                sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }

            return sha256.Hash;
        }
        """);

        source.AppendLine("}");

        context.AddSource($"{config.Name ?? @class.Identifier.Text}.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));


    }

    private string GenerateFields(
    List<FieldDeclarationSyntax?> enviedFields, Envied config, IDictionary<string, string>? env, GeneratorExecutionContext context)
    {
        var source = new StringBuilder();

        foreach (var enviedField in enviedFields)
        {

            var attribute = enviedField?.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .FirstOrDefault(attribute => attribute.Name.ToString() == "EnviedField");

            if (attribute == null)
                continue;

            var name = GetAttributeArgumentValue<string>(attribute, "name")
                       ?? enviedField.Declaration.Variables.First().Identifier.Text;

            if (config.UseConstantCase) name = name.ToUpperInvariant();
            if (env == null || (!env.TryGetValue(name, out string? value) || !GetAttributeArgumentValue<string>(attribute, "defaultValue", out value)) && !config.AllowOptionalFields)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "ENV002",
                        "Missing Environment Variable",
                        $"The environment variable {name} is missing.",
                        "Usage",
                        DiagnosticSeverity.Warning,
                        true),
                    enviedField.GetLocation()));
                continue;
            }

            value ??= enviedField.Declaration.Variables.First().Initializer.Value.ToString();

            var (fieldName, fieldValue) = GetAttributeArgumentValue<bool?>(attribute, "obfuscate") ?? config.Obfuscate
                ? EncryptField(name, value, config.RandomSeed)
                : (name, value);

            var fieldType = enviedField.Declaration.Type.ToString();
            source.AppendLine($"public static readonly {fieldType} {fieldName} = EnviedHelper.Decrypt(\"{fieldValue}\", Key);");
        }

        return source.ToString();
    }



    private (string FieldName, string FieldValue) EncryptField(string name, string value, int? seed)
    {
        var key = GetKey();
        var iv = new byte[12];
        var random = new Random(seed ?? Random.Shared.Next());

        random.NextBytes(key);
        random.NextBytes(iv);
        using Aes aes = Aes.Create();

        aes.Key = key;
        aes.IV = iv;

        using var encryptor = aes.CreateEncryptor();
        var encrypted = $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(encryptor.TransformFinalBlock(Encoding.UTF8.GetBytes(value), 0, value.Length))}";

        var bytes = Encoding.UTF8.GetBytes(name);
        random.NextBytes(bytes);
        return (Convert.ToBase64String(bytes), encrypted);
    }

    private static T? GetAttributeArgumentValue<T>(AttributeSyntax attribute, string argumentName)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name?.ToString() == argumentName);
        if (argument?.Expression is LiteralExpressionSyntax literalExpression &&
            literalExpression.Token.Value is T value)
        {
            return value;
        }
        return default;
    }

    private static bool GetAttributeArgumentValue<T>(AttributeSyntax attribute, string argumentName, out T value)
    {
        value = GetAttributeArgumentValue<T>(attribute, argumentName);
        return value != null;
    }

    private byte[] GetKey()
    {
        using SHA256 sha256 = SHA256.Create();
        foreach (var source in Sources)
        {
            var bytes = File.ReadAllBytes(source);
            sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        return sha256.Hash;
    }

}

