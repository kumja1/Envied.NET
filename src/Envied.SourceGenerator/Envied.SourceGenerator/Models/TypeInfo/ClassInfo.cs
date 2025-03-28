using Envied.SourceGenerator.Models;

namespace Envied.SourceGenerator.Models.TypeInfo;

internal readonly record struct ClassInfo
{
    public string Name { get; init; }
    public string Namespace { get; init; }

    public ValueEquatableArray<PropertyInfo> Properties { get; init; }

    public ValueEquatableArray<string> Modifiers { get; init; }

    public static ClassInfo Empty =>
        new()
        {
            Name = string.Empty,
            Namespace = string.Empty,
            Properties = Array.Empty<PropertyInfo>(),
            Modifiers = Array.Empty<string>(),
        };
}
