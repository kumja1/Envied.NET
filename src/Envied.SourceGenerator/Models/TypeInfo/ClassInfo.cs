namespace Envied.SourceGenerator.Models.TypeInfo;

public readonly record struct ClassInfo
{
    public bool UsePartial { get; init; }
    
    public string Name { get; init; }
    public string Namespace { get; init; }

    public ValueEquatableArray<DiagnosticInfo> Diagnostics { get; init; }
    public ValueEquatableArray<PropertyInfo> Properties { get; init; }
    
    public static ClassInfo Empty =>  new()
    {
        Name = string.Empty,
        Namespace = string.Empty,
        Properties = Array.Empty<PropertyInfo>(),
        Diagnostics = new List<DiagnosticInfo>(),
        UsePartial = false
    };
}