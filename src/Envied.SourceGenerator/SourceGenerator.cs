﻿using System.Text;
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
        var syntaxProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Envied.EnviedAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (context, _) => context)
            .Where(static x => x.TargetNode is not null && x.TargetSymbol is not null);

        Log.LogInfo("Registering syntax provider completed.");

        var outputProvider = context.AnalyzerConfigOptionsProvider
            .Combine(syntaxProvider.Collect())
            .SelectMany((tuple, _) => tuple.Right.Length <= 0 ? [ClassInfo.Empty] : tuple.Right.Select(classInfo => Exec.TransformClass((ClassDeclarationSyntax)classInfo.TargetNode, classInfo.TargetSymbol,
                classInfo.SemanticModel, tuple.Left)));

        context.RegisterSourceOutput(outputProvider, (sourceContext, classInfo) =>
        {
            if (classInfo.Diagnostics.Length > 0)
            {
                Console.WriteLine($"Class {classInfo.Name} has {classInfo.Diagnostics.Length} diagnostics.");
                Log.LogInfo($"Class {classInfo.Name} has {classInfo.Diagnostics.Length} diagnostics.");
                foreach (var diagnostic in classInfo.Diagnostics)
                {
                    sourceContext.ReportDiagnostic(diagnostic.ToDiagnostic());
                }
            }

            GenerateClass(sourceContext, classInfo);
        });
    }

    private void GenerateClass(SourceProductionContext context, ClassInfo classInfo)
    {
        var fieldsSource = GenerateFields(classInfo);
        var sb = new StringBuilder($"""
using Envied.Utils;
using System.Reflection;

namespace {classInfo.Namespace};  

""");

        sb.AppendLine(!classInfo.UsePartial ? @$"public static class {classInfo.Name}_Generated " : @$"public static partial class {classInfo.Name} ");
        sb.AppendLine($$"""
                        {
                                    {{fieldsSource}}
                                }
                        """);
        
        context.AddSource($"{classInfo.Name}.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string GenerateFields(ClassInfo classInfo)
    {
        if (classInfo == ClassInfo.Empty) 
            return string.Empty;

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
        Console.WriteLine($"Generating field for property {property.Name}...");
        var privateField = property.Name.ToLower();
        return $"\npublic static {(classInfo.UsePartial ? "partial " : string.Empty)}{property.Type.Name} {property.Name} => _{privateField};\n\nprivate static readonly {property.Type.Name} _{privateField} = {property.Value};";
    }
}
