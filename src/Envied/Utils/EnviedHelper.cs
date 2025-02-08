using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Envied.Common.Extensions;
using Envied.Common.Utils;

namespace Envied.Utils
{
    public static class EnviedHelper
    {
        public static string Decrypt(string value, byte[] key)
        {
            byte[] encrypted = Convert.FromBase64String(value);

            using var aes = Aes.Create();
            aes.Key = key;

            Span<byte> iv = stackalloc byte[16];
            encrypted.AsSpan(0, 16).CopyTo(iv);
            aes.IV = iv.ToArray();

            int cipherTextLength = encrypted.Length - 16;
            Span<byte> cipherText = stackalloc byte[cipherTextLength];
            encrypted.AsSpan(16, cipherTextLength).CopyTo(cipherText);

            using var decryptor = aes.CreateDecryptor();
            byte[] decrypted = decryptor.TransformFinalBlock(cipherText.ToArray(), 0, cipherTextLength);

            return Encoding.UTF8.GetString(decrypted);
        }

        public static byte[] DeriveKey(Assembly assembly)
        {
            var name = assembly.GetName();

            var types = assembly.GetTypes().Where(t => !t.Name.EndsWith("Generated")).ToList();

            Span<string> hashes = new string[types.Count];
            for (var i = 0; i < types.Count; i++)
            {
                hashes[i] = HashType(types[i]);
            }

            hashes.Sort(StringComparer.Ordinal);

            return HashHelper.CombineHashes(name.Name, name.Version?.ToString(), hashes);
        }

        private static string HashType(Type type)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                          BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.Name is not (".ctor" or ".cctor" or "value__"))
                .ToList();

            Span<string> relevantMembers = new string[members.Count];
            for (int i = 0; i < members.Count; i++)
            {
                var member = members[i];
                relevantMembers[i] = member is MethodInfo method
                    ? FormatMethod(method)
                    : $"F:{member.Name}";
            }

            relevantMembers.Sort(StringComparer.Ordinal);

            return Convert.ToBase64String(HashHelper.HashMembers(type.Name, relevantMembers));
        }

        private static string FormatMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return $"M:{method.Name}:{Nullable.GetUnderlyingType(method.ReturnType)?.Name ?? method.ReturnType.Name}";
            }

            var sb = new StringBuilder(method.Name.Length + 16 + parameters.Length * 16);
            sb.Append(method.Name).Append(':').Append(method.ReturnType.Name).Append(':');

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append(parameters[i].Name)
                  .Append(':')
                  .Append(parameters[i].ParameterType.Name);
            }

            return sb.ToString();
        }
    }
}
