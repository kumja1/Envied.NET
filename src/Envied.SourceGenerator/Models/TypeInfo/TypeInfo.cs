using Envied.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Envied.SourceGenerator.Models.TypeInfo;

public readonly record struct TypeInfo
{
    public string Name { get; init; }
    public bool IsEnum { get; init; }

    public bool IsNullable { get; init; }

    public ValueEquatableArray<string> EnumMembers { get; init; }

    public static TypeInfo From(ITypeSymbol type) => new()
    {
        Name = TypeHelper.GetUnderlyingType(type).ToDisplayString(),
        IsEnum = type.TypeKind == TypeKind.Enum,
        EnumMembers = type.TypeKind == TypeKind.Enum ? type.GetMembers().Select(m => m.Name).ToArray() : [],
        IsNullable = type.SpecialType == SpecialType.System_Nullable_T
    };
}