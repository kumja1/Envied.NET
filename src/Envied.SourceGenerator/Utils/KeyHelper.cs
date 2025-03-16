using System.Buffers;
using System.Text;
using Envied.Common.Utils;
using Microsoft.CodeAnalysis;
using Envied.Common.Extensions;

namespace Envied.SourceGenerator.Utils;

public static class KeyHelper
{
   public static byte[] DeriveKey(IAssemblySymbol assembly)
{
    if (assembly is null)
        throw new ArgumentNullException(nameof(assembly));

    Log.LogInfo($"Deriving key for assembly: {assembly.Name}");

    List<INamedTypeSymbol> types = GetAllTypes(assembly?.GlobalNamespace)?.ToList() ?? throw new InvalidOperationException("Failed to get types.");
    Log.LogInfo($"Found {types.Count} types in assembly: {assembly.Name}");

    var hashes = ArrayPool<string>.Shared.Rent(types.Count);

    try
    {
        for (int i = 0; i < types.Count; i++)
        {
            var type = types[i];
            if (type is null)
            {
                Log.LogError($"Type at index {i} is null.");
                continue;
            }

            hashes[i] = HashType(type);
        }

        Array.Sort(hashes, 0, types.Count, StringComparer.Ordinal);
        var combinedHash = HashHelper.CombineHashes(assembly.Name, assembly?.Identity?.Version?.ToString() , hashes.AsSpan(0, types.Count));
        return combinedHash;
    }
    finally
    {
        ArrayPool<string>.Shared.Return(hashes, clearArray: true);
    }
}

    private static string HashType(INamedTypeSymbol type)
    {
        if (type is null)
            throw new ArgumentNullException(nameof(type));
            
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
        if (method is null)
            throw new ArgumentNullException(nameof(method));
            
        var returnType = TypeHelper.GetUnderlyingType(method.ReturnType).Name;
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
            sb.Append(param.Name).Append(':').Append(TypeHelper.GetUnderlyingType(param.Type).Name);
        }

        return sb.ToString();
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        if (ns is null)
            throw new ArgumentNullException(nameof(ns));
            
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
}