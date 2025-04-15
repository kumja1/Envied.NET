using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Envied.SourceGenerator.Analyzer;

internal static class DiagnosticMessages
{
    internal static DiagnosticDescriptor ClassMustBeStatic =>
        new(
            "ENV001",
            "Class not static",
            "Class must be static",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor ClassMustBePartial =>
        new(
            "ENV002",
            "Class must be partial",
            "Class must be partial in newer frameworks",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor PropertyMustBeStatic =>
        new(
            "ENV003",
            "Property not static",
            "Property must be static",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor PropertyMustBePartial =>
        new(
            "ENV004",
            "Property must be partial",
            "Property must be partial in newer frameworks",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor EnvFileNotFound =>
        new(
            "ENV005",
            "Missing environment file",
            "Environment file not found",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor ProjectRootNotFound =>
        new(
            "ENV007",
            "Project root not found",
            "Project root directory not found",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor InvalidTypeConversion =>
        new(
            "ENV008",
            "Invalid type conversion",
            "Type {0} cannot be converted to {1}",
            "TypeConversion",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor EnvVariableNotFound =>
        new(
            "ENV007",
            "Missing Environment Variable",
            "Environment variable '{0}' could not be found.",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor OptionalValueTypesMustBeNullable =>
        new(
            "ENV009",
            "Non-string optional fields must be nullable",
            "Optional field '{0}' of type {1} must be nullable",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =
        ImmutableArray.Create(
            EnvVariableNotFound,
            EnvFileNotFound,
            ProjectRootNotFound,
            InvalidTypeConversion,
            PropertyMustBePartial,
            PropertyMustBeStatic,
            ClassMustBePartial,
            ClassMustBeStatic,
            OptionalValueTypesMustBeNullable
        );
}
