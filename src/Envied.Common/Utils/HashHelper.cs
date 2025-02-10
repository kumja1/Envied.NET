using System.Security.Cryptography;
using System.Text;

namespace Envied.Common.Utils;

public static class HashHelper
{
    public static readonly SHA256 Hasher = SHA256.Create();
    public static readonly Encoding TextEncoder = Encoding.UTF8;

    public static byte[] CombineHashes(string name, string version, Span<string> typeHashes)
    {
        var sb = new StringBuilder(name.Length + version.Length + typeHashes.Length * 64);
        sb.Append(name).Append('|').Append(version);

        foreach (var hash in typeHashes)
            sb.Append('|').Append(hash);

        var combined = sb.ToString();
        return Hasher.ComputeHash(TextEncoder.GetBytes(combined));
    }


    public static byte[] HashMembers(string typeName, Span<string> members)
    {
        var sb = new StringBuilder(typeName.Length + 1);
        sb.Append(typeName).Append('|');

        foreach (var member in members)
            sb.Append(member.ToLower()).Append(',');

        if (members.Length > 0)
            sb.Length--; // Remove last comma

        return Hasher.ComputeHash(TextEncoder.GetBytes(sb.ToString()));
    }


}