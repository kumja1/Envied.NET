using System.Buffers;
using System.Text;
using Envied.Common.Utils;
using Microsoft.CodeAnalysis;
using Envied.Common.Extensions;

namespace Envied.SourceGenerator.Utils;

public static class GeneratorHelper
{
    public static byte[] DeriveKey(IAssemblySymbol assembly)
    {
        List<INamedTypeSymbol> types = [.. GetAllTypes(assembly.GlobalNamespace)];
        var hashes = ArrayPool<string>.Shared.Rent(types.Count);

        try
        {
            for (int i = 0; i < types.Count; i++)
            {
                hashes[i] = HashType(types[i]);
            }
            Array.Sort(hashes, 0, types.Count, StringComparer.Ordinal);
            var combinedHash = HashHelper.CombineHashes(assembly.Name, assembly.Identity.Version.ToString(), hashes.AsSpan(0, types.Count));
            return combinedHash;
        }
        finally
        {
            ArrayPool<string>.Shared.Return(hashes, clearArray: true);
        }
    }

    private static string HashType(INamedTypeSymbol type)
    {
        var members = type
        .GetMembers()
        .Where(static m => m.Name is not (".ctor" or ".cctor") && m.DeclaredAccessibility == Accessibility.Public)
        .ToList();
        
        Span<string> relevantMembers = new string[members.Count];
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            relevantMembers[i] = member is IMethodSymbol method ? FormatMethod(method) : $"F:{member.Name}";
        }

        relevantMembers.Sort(StringComparer.Ordinal);
        var hash = Convert.ToBase64String(HashHelper.HashMembers(type.Name, relevantMembers));
        return hash;
    }

    private static string FormatMethod(IMethodSymbol method)
    {
        var returnType = GetUnderlyingType(method.ReturnType).Name;
        if (method.Parameters.Length == 0)
        {
            return $"M:{method.Name}:{returnType}";
        }
      
        var sb = new StringBuilder();
        sb.Append("M:").Append(method.Name).Append(':').Append(returnType).Append(':');

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var param = method.Parameters[i];
            sb.Append(param.Name).Append(':').Append(GetUnderlyingType(param.Type).Name);
        }

        return sb.ToString();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        var stack = new Stack<INamespaceOrTypeSymbol>();
        stack.Push(ns);

        INamespaceOrTypeSymbol? current;
        while (stack.Count > 0 && (current = stack.Pop()) != null)
        {
            switch (current)
            {
                case INamedTypeSymbol type:
                    yield return type;

                    foreach (var nested in type.GetTypeMembers())
                        stack.Push(nested);
                    break;

                case INamespaceSymbol namespaceSymbol:
                    foreach (var member in namespaceSymbol.GetMembers())
                        stack.Push(member);
                    break;
            }
        }
    }

    public static string GetTypeConversionFor(string value, INamedTypeSymbol type)
    {
        var underlyingType = GetUnderlyingType(type);
        var typeString = underlyingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        string actualValue = value.Trim('"');
        if (type.EnumUnderlyingType != null)
        {
            return value.Contains("EnviedHelper.Decrypt")
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

    private static ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        return type switch
        {
            INamedTypeSymbol namedType when namedType.IsGenericType && namedType.Name == "Nullable" && namedType.TypeArguments.Length == 1 => namedType.TypeArguments[0],
            _ => type
        };
    }

    public static bool IsValidTypeConversion(string value, ITypeSymbol type)
    {
        var underlyingType = GetUnderlyingType(type).ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        bool isValid = TypeParserCache.TryParse(underlyingType, value);
        if (!isValid)
            isValid = value.Contains("EnviedHelper.Decrypt") || type.TypeKind == TypeKind.Enum && type.GetMembers().Any(m => m.Name == value);
        
        return isValid;
    }
}

static class TypeParserCache
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

    public static bool TryParse(string typeName, string value)
    {
        return Parsers.TryGetValue(typeName, out var parser) && parser(value);
    }
}
