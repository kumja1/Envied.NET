namespace Envied.SourceGenerator.Models.TypeInfo;

public readonly record struct ClassInfo
{
    public bool UsePartial { get; init; }
    
    public string Name { get; init; }
    public string Namespace { get; init; }

    public ValueEquatableArray<DiagnosticInfo> Diagnostics { get; init; }
    public ValueEquatableArray<PropertyInfo> Properties { get; init; }
}