namespace Envied.SourceGenerator.Models.TypeInfo;

public readonly record struct PropertyInfo
{

    public string Name { get; init; }

    public string Value { get; init; }

    public TypeInfo Type { get; init; }

    public bool IsObfuscated { get; init; }

    public ValueEquatableArray<string> Modifiers { get; init; }
}