using Microsoft.CodeAnalysis;

namespace Envied.SourceGenerator;

public static class DiagnosticMessages
{
    public static readonly DiagnosticDescriptor MissingEnvFile = new(
        id: "ENV001",
        title: "Missing Environment File",
        messageFormat: "The environment file '{0}' is missing.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingEnvVariable = new(
        id: "ENV002",
        title: "Missing Environment Variable",
        messageFormat: "The environment variable '{0}' is missing.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ClassMustBeStatic = new(
        id: "ENV004",
        title: "Class Must Be {0} and Static",
        messageFormat: "The class '{0}' must be declared {1} and static.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidTypeConversion = new(
        id: "ENV005",
        title: "Invalid Type Conversion",
        messageFormat: "Cannot convert '{0}' to type '{1}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProjectRootNotFound = new(
        id: "ENV006",
        title: "Project Root Not Found",
        messageFormat: "Could not determine project root directory",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FieldMustBeStatic = new(
        id: "ENV007",
        title: "Field Must Be Static",
        messageFormat: "The field '{0}' must be declared static. Skipping {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonStringOptionalFieldsMustBeNullable = new(
        id: "ENV008",
        title: "Non-string optional fields must be nullable",
        messageFormat: "Optional field '{0}' of type {1} must be nullable",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

