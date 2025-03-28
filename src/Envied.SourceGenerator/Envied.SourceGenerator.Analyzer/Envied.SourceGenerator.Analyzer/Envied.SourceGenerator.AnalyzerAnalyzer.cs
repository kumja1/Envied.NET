using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Envied.SourceGenerator.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EnviedAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENV";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        DiagnosticMessages.SupportedDiagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationStartAnalysisContext context)
    {
        var env = EnvironmentHelper.LoadEnvironment()
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.Attribute);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        if (attributeSyntax.Name.ToString() != "Envied")
            return;

        var classDecl = attributeSyntax.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return;
        
    }
}
