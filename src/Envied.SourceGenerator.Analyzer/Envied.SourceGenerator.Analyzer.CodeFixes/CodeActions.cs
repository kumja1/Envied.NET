using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Envied.SourceGenerator.Analyzer.CodeFix;

internal static class CodeActions
{
    public const string AddPartialModifierTitle = "Add partial modifier";
    public const string AddStaticModifierTitle = "Add static modifier";

    public static async Task<Document> AddPartialModifier(
        CodeFixContext context,
        SyntaxNode node,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (node is not MemberDeclarationSyntax memberDeclaration)
            return context.Document;

        var editor = await DocumentEditor.CreateAsync(context.Document, token);

        token.ThrowIfCancellationRequested();
        var newClassDeclaration = memberDeclaration.AddModifiers(
            SyntaxFactory.Token(SyntaxKind.PartialKeyword)
        );
        editor.ReplaceNode(memberDeclaration, newClassDeclaration);
        token.ThrowIfCancellationRequested();

        return editor.GetChangedDocument();
    }

    public static async Task<Document> AddStaticModifier(
        CodeFixContext context,
        SyntaxNode node,
        CancellationToken token
    )
    {
        token.ThrowIfCancellationRequested();
        if (node is not MemberDeclarationSyntax memberDeclaration)
            return context.Document;

        var editor = await DocumentEditor.CreateAsync(context.Document, token);
        token.ThrowIfCancellationRequested();
        var newClassDeclaration = memberDeclaration.AddModifiers(
            SyntaxFactory.Token(SyntaxKind.StaticKeyword)
        );
        editor.ReplaceNode(memberDeclaration, newClassDeclaration);
        token.ThrowIfCancellationRequested();

        return editor.GetChangedDocument();
    }
}
