using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using PropertyInfo = Envied.SourceGenerator.Models.TypeInfo.PropertyInfo;
using Envied.SourceGenerator.Models.TypeInfo;

namespace Envied.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class EnviedSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Console.WriteLine("Initializing Envied Source Generator...");
        Log.LogInfo("Initializing Envied Source Generator...");
        Log.LogInfo("Registering syntax provider...");

        var syntaxProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Envied",
            predicate: static (node, _) => node is ClassDeclarationSyntax and not null,
            transform: static (context, _) =>
            {
                Log.LogInfo($"Transforming class {context.TargetSymbol?.Name}...");
                Log.LogInfo($"Class {context.TargetNode?.ToFullString()} is a class declaration");
                return (Node: (ClassDeclarationSyntax?)context.TargetNode,
                        context.TargetSymbol,
                        context.SemanticModel);
            })
            .Where(static x => x.Node is not null && x.TargetSymbol is not null);

        Log.LogInfo("Registering syntax provider completed.");

        var outputProvider = context.AnalyzerConfigOptionsProvider
            .Combine(syntaxProvider.Collect())
            .Select((tuple, _) =>
            {
                if (tuple.Left == null) throw new ArgumentNullException(nameof(tuple.Left));
                if (tuple.Right == null) throw new ArgumentNullException(nameof(tuple.Right));

                if (tuple.Right.Length > 0)
                {
                    foreach (var classInfo in tuple.Right)
                    {
                        if (!Exec.TransformClass(classInfo.Node, classInfo.TargetSymbol, classInfo.SemanticModel, tuple.Left, out var equatableModel)) continue;
                        return equatableModel;
                    }
                }
                return new ClassInfo
                {
                    Name = string.Empty,
                    Namespace = string.Empty,
                    Properties = new PropertyInfo[0],
                    Diagnostics = [],
                    UsePartial = false
                };
            });

        context.RegisterSourceOutput(outputProvider, (context, classInfo) =>
        {
                        Log.Flush(context);

            if (classInfo == null) throw new ArgumentNullException(nameof(classInfo));

            Console.WriteLine($"Processing class {classInfo.Name}...");
            Log.LogInfo($"Processing class {classInfo.Name}...");
            bool hasError = false;
            if (classInfo.Diagnostics.Length > 0)
            {
                Console.WriteLine($"Class {classInfo.Name} has {classInfo.Diagnostics.Length} diagnostics.");
                Log.LogInfo($"Class {classInfo.Name} has {classInfo.Diagnostics.Length} diagnostics.");
                foreach (var diagnostic in classInfo.Diagnostics)
                {
                    Console.WriteLine($"Diagnostic: {diagnostic.ToDiagnostic().GetMessage()}");
                    Log.LogInfo($"Diagnostic: {diagnostic.ToDiagnostic().GetMessage()}");
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                    {
                        hasError = true;
                        break;
                    }
                }
                Log.LogInfo($"Class {classInfo.Name} has warnings. Skipping generation.");
            }
            if (!hasError)
                GenerateClass(context, classInfo);
        });
    }

    private void GenerateClass(SourceProductionContext context, ClassInfo classInfo)
    {
        if (classInfo == null) throw new ArgumentNullException(nameof(classInfo));

        Console.WriteLine($"Generating class {classInfo.Name}...");
        var fieldsSource = GenerateFields(classInfo);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace tes;  

""");

        sb.AppendLine(!classInfo.UsePartial ? @$"public static class {classInfo.Name}_Generated " : @$"public static partial class {classInfo.Name} ");
        sb.AppendLine(@$"{{
            {fieldsSource}
        }}");

        Log.LogInfo($"Generated source for {classInfo.Name}");
        context.AddSource($"{classInfo.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GenerateFields(ClassInfo classInfo)
    {
        if (classInfo == null) throw new ArgumentNullException(nameof(classInfo));

        Console.WriteLine($"Generating fields for {classInfo.Name}...");
        var fieldsSource = new StringBuilder();
        int obfuscatedFields = 0;

        Log.LogInfo($"Generating fields for {classInfo.Name} with {classInfo.Properties.Length} properties.");

        if (classInfo.Properties.Length == 0)
            return string.Empty;

        foreach (var propertyInfo in classInfo.Properties)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));

            Console.WriteLine($"Property {propertyInfo.Name} is {(propertyInfo.IsObfuscated ? "obfuscated" : "not obfuscated")}.");
            Log.LogInfo($"Property {propertyInfo.Name} is {(propertyInfo.IsObfuscated ? "obfuscated" : "not obfuscated")}.");
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
        if (classInfo == null) throw new ArgumentNullException(nameof(classInfo));
        if (property == null) throw new ArgumentNullException(nameof(property));

        Console.WriteLine($"Generating field for property {property.Name}...");
        var privateField = property.Name.ToLower();
        return $"\npublic static {(!classInfo.UsePartial ? string.Empty : "partial ")}{property.Type.Name} {property.Name} => _{privateField};\n\nprivate static readonly {property.Type.Name} _{privateField} = {property.Value};";
    }
}
