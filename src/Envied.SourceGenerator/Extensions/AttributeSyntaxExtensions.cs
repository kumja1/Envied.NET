using System.Globalization;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Envied.SourceGenerator.Extensions;

public static class AttributeSyntaxExtensions
{
    public static T? GetArgument<T>(this AttributeSyntax attribute, string name, T? defaultValue = default)
    {
        var argument = attribute.ArgumentList?.Arguments
            .FirstOrDefault(arg => string.Equals((arg.NameColon?.Name ?? arg.NameEquals?.Name)!.Identifier.Text, name, StringComparison.OrdinalIgnoreCase));
         
        if (argument?.Expression is LiteralExpressionSyntax literal)
            return (T)literal.Token.Value;
        return defaultValue;
    }
}
