using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Envied.Common.Utils;

using Envied.Common.Extensions;

namespace Envied.SourceGenerator.Utils;

public static class RuntimeKeyHelper
{
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
            return HashHelper.CombineHashes(name.Name, name.Version?.ToString(), hashes.AsSpan(0, types.Length));
        }
        finally
        {
            ArrayPool<string>.Shared.Return(hashes);
        }
    }


    private static string HashType(Type type)
    {
        var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                    BindingFlags.Static | BindingFlags.DeclaredOnly);


        Span<string> relevantMembers = [];
        for (int i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.Name is ".ctor" or ".cctor" or "value__") continue;

            relevantMembers[i] = member is MethodInfo method
                ? FormatMethod(method)
                : member.Name;
        }


        relevantMembers.Sort(StringComparer.Ordinal);
        return Convert.ToBase64String(HashHelper.HashMembers(type.Name, relevantMembers));
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



}