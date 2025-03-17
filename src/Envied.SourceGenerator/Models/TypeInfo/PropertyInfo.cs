namespace Envied.SourceGenerator.Models.TypeInfo;

internal readonly record struct PropertyInfo
{

    public string Name { get; init; }

    public string Value { get; init; }

    public TypeInfo Type { get; init; }

    public LocationInfo Location { get; init; }


    public bool IsObfuscated { get; init; }

    public ValueEquatableArray<string> Modifiers { get; init; }
}