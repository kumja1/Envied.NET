using Microsoft.CodeAnalysis;
using Envied.SourceGenerator.Models;

namespace Envied.SourceGenerator;

public static class DiagnosticMessages
{
    public static DiagnosticInfo ClassMustBeStatic => new(
        "ENV004",
        "Class not static",
        "Class must be static",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo ClassMustBePartial => new(
        "ENV004",
        "Class must be partial",
        "Class must be partial in newer frameworks",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo FieldMustBeStatic => new(
        "ENV007",
        "Field not static",
        "Field must be static",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo FieldMustBePartial => new(
        "ENV007",
        "Field must be partial",
        "Field must be partial in newer frameworks",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo MissingEnvFile => new(
        "ENV001",
        "Missing environment file",
        "Environment file not found",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo ProjectRootNotFound => new(
        "ENV006",
        "Project root not found",
        "Project root directory not found",
        DiagnosticSeverity.Error);

    public static DiagnosticInfo InvalidTypeConversion => new(
       "ENV007",
       "Invalid type conversion",
       "Type {0} cannot be converted to {1}",
       DiagnosticSeverity.Error);

    public static DiagnosticInfo MissingEnvironmentVariable => new(
        "ENV002",
        "Missing Environment Variable",
        "The environment variable '{0}' is missing.",
        DiagnosticSeverity.Error
    );
}
