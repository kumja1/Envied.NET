using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using PropertyInfo = Envied.SourceGenerator.Models.TypeInfo.PropertyInfo;
using Envied.SourceGenerator.Models.TypeInfo;

namespace Envied.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class EnviedSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetFrameworkProvider = context.AnalyzerConfigOptionsProvider
            .Select(Exec.IsOlderProject);

        var syntaxProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Envied.EnviedAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (context, token) => Exec.TransformClass((ClassDeclarationSyntax)context.TargetNode, context.TargetSymbol, context.SemanticModel, token))
            .Combine(targetFrameworkProvider);
        context.RegisterSourceOutput(syntaxProvider, RunGenerator);
    }


    private void RunGenerator(SourceProductionContext context, (ClassInfo classInfo, bool isOlderProject) tuple)
    {
        var (classInfo, isOlderProject) = tuple;

        if (!classInfo.Modifiers.Contains("static"))
        {
            if (!isOlderProject && !classInfo.Modifiers.Contains("partial"))
                context.ReportDiagnostic(DiagnosticMessages.ClassMustBePartial.WithLocation(classInfo.Location).ToDiagnostic());
            context.ReportDiagnostic(DiagnosticMessages.ClassMustBeStatic.WithLocation(classInfo.Location).ToDiagnostic());
        }

        if (classInfo.Diagnostics.Length > 0)
        {
            foreach (var diagnostic in classInfo.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }
        }

        if (classInfo == ClassInfo.Empty)
            return;

        if (classInfo.Properties.Length == 0)
            return;

        GenerateClass(context, classInfo, isOlderProject);
    }


    private void GenerateClass(SourceProductionContext context, ClassInfo classInfo, bool isOlderProject)
    {
        var fieldsSource = GenerateFields(classInfo, context, isOlderProject);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace {classInfo.Namespace};  

""");

        sb.AppendLine(isOlderProject ? @$"public static class {classInfo.Name}_Generated " : @$"public static partial class {classInfo.Name} ");
        sb.AppendLine($$"""
                        {
                                    {{fieldsSource}}
                        }
                        """);

        context.AddSource($"{classInfo.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GenerateFields(ClassInfo classInfo, SourceProductionContext context, bool isOlderProject)
    {
        if (classInfo == ClassInfo.Empty)
            return string.Empty;

        Console.WriteLine($"Generating fields for {classInfo.Name}...");
        var fieldsSource = new StringBuilder();
        int obfuscatedFields = 0;

        if (classInfo.Properties.Length == 0)
            return string.Empty;

        foreach (var propertyInfo in classInfo.Properties)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));

            Console.WriteLine($"Property {propertyInfo.Name} is {(propertyInfo.IsObfuscated ? "obfuscated" : "not obfuscated")}.");
            if (propertyInfo.IsObfuscated)
                obfuscatedFields++;

            var fieldSource = GenerateField(propertyInfo, context, isOlderProject);
            if (string.IsNullOrEmpty(fieldSource))
                continue;

            fieldsSource.AppendLine(fieldSource);
        }

        if (obfuscatedFields > 0)
            fieldsSource.Insert(0, $"  private static readonly byte[] _key = RuntimeKeyHelper.DeriveKey(Assembly.GetCallingAssembly());\n");

        return fieldsSource.ToString();
    }

    private static string GenerateField(PropertyInfo property, SourceProductionContext context, bool isOlderProject)
    {
        if (!property.Modifiers.Contains("static"))
        {
            if (!isOlderProject && !property.Modifiers.Contains("partial"))
                context.ReportDiagnostic(DiagnosticMessages.FieldMustBePartial.WithLocation(property.Location).ToDiagnostic());
            context.ReportDiagnostic(DiagnosticMessages.FieldMustBeStatic.WithLocation(property.Location).ToDiagnostic());
            return string.Empty;
        }

        var privateField = property.Name.ToLower();
        return $"\npublic static {(!isOlderProject ? "partial " : string.Empty)}{property.Type.Name} {property.Name} => _{privateField};\n\nprivate static readonly {property.Type.Name} _{privateField} = {property.Value};";
    }
}
