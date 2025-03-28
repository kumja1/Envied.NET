using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Envied.SourceGenerator.Extensions;

internal static class AttributeSyntaxExtensions
{
    internal static T? GetArgument<T>(this AttributeSyntax attribute, string name, T? defaultValue = default)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => string.Equals((arg.NameColon?.Name ?? arg.NameEquals?.Name)!.Identifier.Text, name, StringComparison.OrdinalIgnoreCase));

        return argument?.Expression is LiteralExpressionSyntax { Token.Value: T value } ? value : defaultValue;
    }
}
