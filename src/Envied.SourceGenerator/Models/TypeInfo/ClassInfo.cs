namespace Envied.SourceGenerator.Models.TypeInfo;

internal readonly record struct ClassInfo
{
    public string Name { get; init; }
    public string Namespace { get; init; }

    public LocationInfo Location { get; init; }

    public ValueEquatableArray<DiagnosticInfo> Diagnostics { get; init; }
    public ValueEquatableArray<PropertyInfo> Properties { get; init; }

    public ValueEquatableArray<string> Modifiers { get; init; }

    public static ClassInfo Empty => new()
    {
        Name = string.Empty,
        Location = LocationInfo.Empty,
        Namespace = string.Empty,
        Properties = Array.Empty<PropertyInfo>(),
        Diagnostics = Array.Empty<DiagnosticInfo>(),
        Modifiers = Array.Empty<string>()
    };
}