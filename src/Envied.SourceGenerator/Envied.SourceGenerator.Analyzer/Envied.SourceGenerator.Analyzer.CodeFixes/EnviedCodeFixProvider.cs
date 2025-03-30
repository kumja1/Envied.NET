using System.Collections.Immutable;
using System.Composition;
using Envied.SourceGenerator.Analyzer.CodeFix;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Envied.SourceGenerator.Analyzer.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EnviedCodeFixProvider)), Shared]
public class EnviedCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        DiagnosticMessages.SupportedDiagnostics.Select(d => d.Id).ToImmutableArray();

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var token = context.CancellationToken;

        token.ThrowIfCancellationRequested();
        var root = await context.Document.GetSyntaxRootAsync(token).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();
        if (root is null)
            return;

        token.ThrowIfCancellationRequested();
        foreach (
            var diagnostic in context.Diagnostics.Where(d =>
                d.Id.StartsWith(EnviedAnalyzer.DiagnosticId)
            )
        )
        {
            token.ThrowIfCancellationRequested();

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            switch (diagnostic.ToString())
            {
                case nameof(DiagnosticMessages.ClassMustBePartial):
                case nameof(DiagnosticMessages.PropertyMustBePartial):
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            CodeActions.AddPartialModifierTitle,
                            c => CodeActions.AddPartialModifier(context, node, c)
                        ),
                        diagnostic
                    );
                    break;

                case nameof(DiagnosticMessages.ClassMustBeStatic):
                case nameof(DiagnosticMessages.PropertyMustBeStatic):
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            CodeActions.AddStaticModifierTitle,
                            c => CodeActions.AddStaticModifier(context, node, c)
                        ),
                        diagnostic
                    );

                    break;
            }
            token.ThrowIfCancellationRequested();
        }
    }
}
