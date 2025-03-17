using Microsoft.CodeAnalysis;
using Envied.SourceGenerator.Models.TypeInfo;

namespace Envied.SourceGenerator.Utils;

public static class TypeHelper
{
    public static string GetConversionExpression(string value, INamedTypeSymbol type)
    {
        var underlyingType = GetUnderlyingType(type);
        var typeString = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        string actualValue = value.Trim('"');
        if (type.EnumUnderlyingType != null)
        {
            return value.Contains("RuntimeKeyHelper.Decrypt")
                ? $"({typeString})Enum.Parse(typeof({typeString}), {value})"
                : $"{typeString}.{actualValue}";
        }

        return underlyingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) switch
        {
            "Guid" => $"{typeString}.Parse({value})",
            "DateTime" => $"{typeString}.Parse({value})",
            "TimeSpan" => $"{typeString}.Parse({value})",
            "Version" => $"{typeString}.Parse({value})",
            "Uri" => $"new Uri({value})",
            "string" => $"{value}",
            "int" or "long" or "short" or "byte" or "sbyte" or "uint" or "ulong" or "ushort" or "float" or "double" or "decimal" => actualValue,
            "bool" => actualValue.ToLower(),
            _ => throw new NotSupportedException($"Type '{underlyingType}' is not supported for conversion.")
        };
    }

    public static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        return type switch
        {
            INamedTypeSymbol { IsGenericType: true, Name: "Nullable", TypeArguments.Length: 1 } namedType => namedType.TypeArguments[0],
            _ => type
        };
    }

      public static bool IsValidTypeConversion(string value, ITypeSymbol type)
    {
        var underlyingType = GetUnderlyingType(type).ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        bool isValid = TypeParserCache.TryParse(underlyingType, value);
        if (!isValid)
            isValid = value.Contains("RuntimeKeyHelper.Decrypt") || type.TypeKind == TypeKind.Enum && type.GetMembers().Any(m => m.Name == value);
        
        return isValid;
    }
}

internal static class TypeParserCache
{
    private static readonly Dictionary<string, Func<string, bool>> Parsers = new(StringComparer.Ordinal)
    {
        ["Guid"] = s => Guid.TryParse(s, out _),
        ["DateTime"] = s => DateTime.TryParse(s, out _),
        ["TimeSpan"] = s => TimeSpan.TryParse(s, out _),
        ["Version"] = s => Version.TryParse(s, out _),
        ["Uri"] = s => Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out _),
        ["int"] = s => int.TryParse(s, out _),
        ["long"] = s => long.TryParse(s, out _),
        ["short"] = s => short.TryParse(s, out _),
        ["byte"] = s => byte.TryParse(s, out _),
        ["sbyte"] = s => sbyte.TryParse(s, out _),
        ["uint"] = s => uint.TryParse(s, out _),
        ["ulong"] = s => ulong.TryParse(s, out _),
        ["ushort"] = s => ushort.TryParse(s, out _),
        ["float"] = s => float.TryParse(s, out _),
        ["double"] = s => double.TryParse(s, out _),
        ["decimal"] = s => decimal.TryParse(s, out _),
        ["bool"] = s => bool.TryParse(s, out _),
        ["string"] = _ => true
    };

    public static bool TryParse(string typeName, string value) => Parsers.TryGetValue(typeName, out var parser) && parser(value);
}
