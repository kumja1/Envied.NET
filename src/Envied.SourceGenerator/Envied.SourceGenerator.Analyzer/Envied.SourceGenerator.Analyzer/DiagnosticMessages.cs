using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Envied.SourceGenerator.Analyzer;

internal static class DiagnosticMessages
{
    internal static DiagnosticDescriptor ClassMustBeStatic =>
        new(
            "ENV004",
            "Class not static",
            "Class must be static",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor ClassMustBePartial =>
        new(
            "ENV004",
            "Class must be partial",
            "Class must be partial in newer frameworks",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor MemberMustBeStatic =>
        new(
            "ENV007",
            "Member not static",
            "Member must be static",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor MemberMustBePartial =>
        new(
            "ENV007",
            "Member must be partial",
            "Member must be partial in newer frameworks",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor MissingEnvFile =>
        new(
            "ENV001",
            "Missing environment file",
            "Environment file not found",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor ProjectRootNotFound =>
        new(
            "ENV006",
            "Project root not found",
            "Project root directory not found",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor InvalidTypeConversion =>
        new(
            "ENV007",
            "Invalid type conversion",
            "Type {0} cannot be converted to {1}",
            "TypeConversion",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor MissingEnvVariable =>
        new(
            "ENV002",
            "Missing Environment Variable",
            "The environment variable '{0}' is missing.",
            "Configuration",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static DiagnosticDescriptor OptionalValueTypesMustBeNullable =>
        new(
            "ENV008",
            "Non-string optional fields must be nullable",
            "Optional field '{0}' of type {1} must be nullable",
            "Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

    internal static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =
        ImmutableArray.Create(
            MissingEnvVariable,
            MissingEnvFile,
            ProjectRootNotFound,
            InvalidTypeConversion,
            MemberMustBePartial,
            MemberMustBeStatic,
            ClassMustBePartial,
            ClassMustBeStatic,
            OptionalValueTypesMustBeNullable
        );
}
