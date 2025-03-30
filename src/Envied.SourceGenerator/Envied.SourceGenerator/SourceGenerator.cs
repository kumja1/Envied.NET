using Envied.SourceGenerator.Common.Utils;
using Envied.SourceGenerator.Models.TypeInfo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Envied.SourceGenerator;

[Generator(LanguageNames.CSharp)]
internal partial class EnviedSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetFrameworkProvider = context.AnalyzerConfigOptionsProvider.Select(
            ProjectHelper.CheckSupportsPartial
        );

        var syntaxProvider = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                "Envied.EnviedAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (context, token) =>
                    TransformClass(
                        (ClassDeclarationSyntax)context.TargetNode,
                        context.TargetSymbol,
                        context.SemanticModel,
                        token
                    )
            )
            .Combine(targetFrameworkProvider);
        context.RegisterSourceOutput(syntaxProvider, SourceOutput);
    }

    private void SourceOutput(
        SourceProductionContext context,
        (ClassInfo classInfo, bool isOlderProject) tuple
    )
    {
        var (classInfo, supportsPartial) = tuple;

        if (!classInfo.Modifiers.Contains("static"))
        {
            // if (!isOlderProject && !classInfo.Modifiers.Contains("partial"))
            //    context.ReportDiagnostic(
            //        DiagnosticMessages
            //            .ClassMustBePartial.WithLocation(classInfo.Location)
            //            .ToDiagnostic()
            //   );
            // context.ReportDiagnostic(
            //     DiagnosticMessages.ClassMustBeStatic.WithLocation(classInfo.Location).ToDiagnostic()
            // );
        }

        //  if (classInfo.Diagnostics.Length > 0)
        //  {
        //      foreach (var diagnostic in classInfo.Diagnostics)
        //      {
        //          context.ReportDiagnostic(diagnostic.ToDiagnostic());
        //     }
        // }

        if (classInfo == ClassInfo.Empty)
            return;

        var source = GenerateSource(classInfo, supportsPartial);
        context.AddSource($"{classInfo.Name}.g.cs", source);
    }
}
