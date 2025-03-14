using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using PropertyInfo = Envied.SourceGenerator.Models.TypeInfo.PropertyInfo;
using Envied.SourceGenerator.Models.TypeInfo;

namespace Envied.SourceGenerator;

[Generator]
public class EnviedSourceGenerator : IIncrementalGenerator
{

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntaxProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName("Envied.EnviedAttribute",
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (context, _) => (Node: (ClassDeclarationSyntax)context.TargetNode, context.TargetSymbol, context.SemanticModel));

        var outputProvider = context.AnalyzerConfigOptionsProvider
            .Combine(syntaxProvider.Collect())
            .SelectMany((tuple, _) => tuple.Right.Select(classInfo => Exec.TransformClass(classInfo.Node, classInfo.TargetSymbol, classInfo.SemanticModel, tuple.Left)));

        context.RegisterSourceOutput(outputProvider, (context, classInfo) =>
        {
            if (classInfo.Diagnostics.Length > 0)
            {
                bool hasError = false;
                foreach (var diagnostic in classInfo.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        hasError = true;
                        break;
                    }
                }

                if (!hasError)
                    GenerateClass(context, classInfo);
            }
        });
    }

    private void GenerateClass(SourceProductionContext context, ClassInfo classInfo)
    {

        var fieldsSource = GenerateFields(classInfo);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace {classInfo.Namespace};  
 
"""
);
        sb.AppendLine(classInfo.UsePartial ? @$"public static class {classInfo.Name}_Generated " : @$"public static partial class {classInfo.Name} ");
        sb.AppendLine(@$"{{
            {fieldsSource}
            }}");

        context.AddSource($"{classInfo.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GenerateFields(ClassInfo classInfo)
    {
        var fieldsSource = new StringBuilder();
        int obfuscatedFields = 0;

        if (classInfo.Properties.Length == 0)
            return string.Empty;

        foreach (var propertyInfo in classInfo.Properties)
        {
            if (propertyInfo.IsObfuscated)
                obfuscatedFields++;

            fieldsSource.AppendLine(GenerateField(classInfo, propertyInfo));
        }

        if (obfuscatedFields > 0)
            fieldsSource.Insert(0, $"private static readonly byte[] _key = RuntimeKeyHelper.DeriveKey(Assembly.GetCallingAssembly());\n");

        return fieldsSource.ToString();
    }

    private static string GenerateField(ClassInfo classInfo, PropertyInfo property)
    {
        var privateField = property.Name.ToLower();
        return $"\npublic static {(classInfo.UsePartial ? string.Empty : "partial ")}{property.Type.Name} {property.Name} => _{privateField};\n\nprivate static readonly {property.Type.Name} _{privateField} = {property.Value};";
    }

}




