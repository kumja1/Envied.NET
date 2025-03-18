using Microsoft.CodeAnalysis;
using Envied.SourceGenerator.Models;

namespace Envied.SourceGenerator;

internal static class DiagnosticMessages
{
    internal static DiagnosticInfo ClassMustBeStatic => new(
        "ENV004",
        "Class not static",
        "Class must be static",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo ClassMustBePartial => new(
        "ENV004",
        "Class must be partial",
        "Class must be partial in newer frameworks",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo FieldMustBeStatic => new(
        "ENV007",
        "Field not static",
        "Field must be static",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo FieldMustBePartial => new(
        "ENV007",
        "Field must be partial",
        "Field must be partial in newer frameworks",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo MissingEnvFile => new(
        "ENV001",
        "Missing environment file",
        "Environment file not found",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo ProjectRootNotFound => new(
        "ENV006",
        "Project root not found",
        "Project root directory not found",
        DiagnosticSeverity.Error);

    internal static DiagnosticInfo InvalidTypeConversion => new(
       "ENV007",
       "Invalid type conversion",
       "Type {0} cannot be converted to {1}",
       DiagnosticSeverity.Error);

    internal static DiagnosticInfo MissingEnvironmentVariable => new(
        "ENV002",
        "Missing Environment Variable",
        "The environment variable '{0}' is missing.",
        DiagnosticSeverity.Error
    );

    internal static DiagnosticInfo OptionalValueTypesMustBeNullable => new(
        "ENV008",
        "Non-string optional fields must be nullable",
        "Optional field '{0}' of type {1} must be nullable",
        DiagnosticSeverity.Error);
}
