using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Envied.SourceGenerator.Utils
{
    public static class EnviedHelper
    {
        private static readonly SHA256 Hasher = SHA256.Create();
        private static readonly Encoding TextEncoder = Encoding.UTF8;

        public static string Decrypt(string value, byte[] key)
        {
            try
            {
                byte[] encrypted = Convert.FromBase64String(value);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = [.. encrypted.Take(16)];

                byte[] decrypted = aes.CreateDecryptor()
                    .TransformFinalBlock(encrypted, 16, encrypted.Length - 16);

                string decryptedString = TextEncoder.GetString(decrypted);

                return decryptedString;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static byte[] GetKey(Assembly assembly)
        {
            var name = assembly.GetName();
            var types = assembly.GetTypes();
            var hashes = ArrayPool<string>.Shared.Rent(types.Length);

            try
            {
                for (int i = 0; i < types.Length; i++)
                {
                    hashes[i] = HashType(types[i]);
                }

                Array.Sort(hashes, 0, types.Length);
                return CombineHashes(name.Name, name.Version?.ToString(), hashes.AsSpan(0, types.Length));
            }
            finally
            {
                ArrayPool<string>.Shared.Return(hashes);
            }
        }

        public static byte[] GetKey(IAssemblySymbol assembly)
        {
            List<INamedTypeSymbol> types = GetAllTypes(assembly.GlobalNamespace).ToList();
            var hashes = ArrayPool<string>.Shared.Rent(types.Count);

            try
            {
                for (int i = 0; i < types.Count; i++)
                {
                    hashes[i] = HashType(types[i]);
                }

                Array.Sort(hashes, 0, types.Count, StringComparer.Ordinal);
                return CombineHashes(assembly.Name, assembly.Identity.Version.ToString(), hashes.AsSpan(0, types.Count));
            }
            finally
            {
                ArrayPool<string>.Shared.Return(hashes);
            }
        }

        private static byte[] CombineHashes(string name, string version, Span<string> typeHashes)
        {
            var sb = new StringBuilder();
            sb.Append(name).Append('|').Append(version);

            foreach (var hash in typeHashes)
                sb.Append('|').Append(hash);

            var combined = sb.ToString();
            return Hasher.ComputeHash(TextEncoder.GetBytes(combined));
        }

        private static string HashType(Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                        BindingFlags.Static | BindingFlags.DeclaredOnly);

            var relevantMembers = new List<string>(members.Length);
            foreach (var member in members)
            {
                if (member.Name is ".ctor" or ".cctor" or "value__") continue;
                relevantMembers.Add(member is MethodInfo method
                    ? FormatMethod(method)
                    : member.Name);
            }

            relevantMembers.Sort();
            return Convert.ToBase64String(HashMembers(type.Name, relevantMembers));
        }

        private static string HashType(INamedTypeSymbol type)
        {
            var members = type.GetMembers();
            var relevantMembers = new List<string>(members.Length);

            foreach (var member in members)
            {
                if (member.Name is ".ctor" or ".cctor" ||
                    member.DeclaredAccessibility != Accessibility.Public) continue;

                relevantMembers.Add(member is IMethodSymbol method
                    ? FormatMethod(method)
                    : member.Name);
            }

            relevantMembers.Sort();
            return Convert.ToBase64String(HashMembers(type.Name, relevantMembers));
        }

        private static byte[] HashMembers(string typeName, List<string> members)
        {
            var sb = new StringBuilder(typeName.Length + 1);
            sb.Append(typeName).Append('|');

            foreach (var member in members)
                sb.Append(member.ToLower()).Append(',');

            if (members.Count > 0)
                sb.Length--; // Remove last comma

            return Hasher.ComputeHash(TextEncoder.GetBytes(sb.ToString()));
        }

        private static string FormatMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
                return $"{method.Name}:{method.ReturnType.Name}";

            var sb = new StringBuilder();
            sb.Append(method.Name).Append(':').Append(method.ReturnType.Name).Append(':');

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(parameters[i].Name).Append(':').Append(parameters[i].ParameterType.Name);
            }

            return sb.ToString();
        }

        private static string FormatMethod(IMethodSymbol method)
        {
            var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            if (method.Parameters.Length == 0)
                return $"{method.Name}:{returnType}";

            var sb = new StringBuilder();
            sb.Append(method.Name).Append(':').Append(returnType).Append(':');

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var param = method.Parameters[i];
                sb.Append(param.Name).Append(':')
                  .Append(param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }

            return sb.ToString();
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
        {
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(ns);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                foreach (var type in current.GetTypeMembers())
                {
                    var typeStack = new Stack<INamedTypeSymbol>();
                    typeStack.Push(type);

                    while (typeStack.Count > 0)
                    {
                        var currentType = typeStack.Pop();
                        yield return currentType;

                        foreach (var nested in currentType.GetTypeMembers())
                            typeStack.Push(nested);
                    }
                }

                foreach (var child in current.GetNamespaceMembers())
                    stack.Push(child);
            }
        }

        public static string GetTypeConversionKeyword(string value, INamedTypeSymbol type)
        {
            return type.EnumUnderlyingType != null ? $"Enum.Parse(typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"{value}\")" :
               type.Name switch
               {
                   "Guid" or "DateTime" or "TimeSpan" or "Version" => $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.Parse(\"{value}\")",
                   "Uri" => $"new Uri(\"{value}\")",
                   "string" => $"\"{value}\"",
                   _ => $"Convert.ChangeType(\"{value}\", typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))"
               };
        }
    }
}
