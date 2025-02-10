using Envied.SourceGenerator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Envied.SourceGenerator.Models;

public readonly record struct ClassInfo
{
    public string Name { get; init; }
    public string Namespace { get; init; }

    public ValueEquatableArray<AttributeInfo> Attributes { get; init; }
    public ValueEquatableArray<string> Modifiers { get; init; }
    public ValueEquatableArray<string> BaseTypes { get; init; }
    public ValueEquatableArray<string> ImplementedInterfaces { get; init; }
    public ValueEquatableArray<FieldInfo> Members { get; init; }

    public static ClassInfo From(ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        return new ClassInfo
        {
            Name = classSymbol.Name,
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            Attributes = classSymbol.GetAttributes().Select(AttributeInfo.From).ToArray(),
            Modifiers = classDeclaration.Modifiers.Select(m => m.Text).ToArray(),
            BaseTypes = classSymbol.BaseType != null ? [classSymbol.BaseType.ToDisplayString()] : Array.Empty<string>(),
            Members = classSymbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Select(FieldInfo.From)
            .ToArray()
        };
    }
}

public readonly record struct FieldInfo
{
    public string Name { get; init; }
    public TypeInfo Type { get; init; }
    public ValueEquatableArray<AttributeInfo> Attributes { get; init; }
    public ValueEquatableArray<string> Modifiers { get; init; }
    public string Initializer { get; init; }

    public static FieldInfo From(IFieldSymbol field) => new()
    {
        Name = field.Name,
        Type = TypeInfo.From(field.Type),
        Attributes = field.GetAttributes().Select(AttributeInfo.From).ToArray(),
        Modifiers = field.DeclaredAccessibility.ToString().Split(' '),
        Initializer = field.HasConstantValue ? field.ConstantValue?.ToString() : null
    };
}

public readonly record struct AttributeInfo
{
    public string Name { get; init; }
    public ValueEquatableArray<(string Name, string Value)> NamedArguments { get; init; }

    public static AttributeInfo From(AttributeData attr) => new()
    {
        Name = attr.AttributeClass.Name,
        NamedArguments = attr.NamedArguments.Select(arg => (arg.Key, arg.Value.ToString())).ToArray()
    };
}


public readonly record struct TypeInfo
{
    public string Name { get; init; }
    public bool IsEnum { get; init; }

    public bool IsNullable { get; init; }


    public ValueEquatableArray<string> EnumMembers { get; init; }

    public static TypeInfo From(ITypeSymbol type) => new()
    {
        Name = GeneratorHelper.GetUnderlyingType(type).ToDisplayString(),
        IsEnum = type.TypeKind == TypeKind.Enum,
        EnumMembers = type.TypeKind == TypeKind.Enum ? type.GetMembers().Select(m => m.Name).ToArray() : [],
        IsNullable = type.SpecialType == SpecialType.System_Nullable_T
    };
}