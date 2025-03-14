using Microsoft.CodeAnalysis;

namespace Envied.SourceGenerator.Models;

public record DiagnosticInfo(
    string Id,
    string Title, 
    string Message,
    DiagnosticSeverity Severity,
    Location? Location = null,
    string[]? MessageArgs = null)
{
    public Diagnostic ToDiagnostic() => Diagnostic.Create(
        new DiagnosticDescriptor(
            Id,
            Title,
            Message,
            "Usage",
            Severity,
            true),
        Location,
        MessageArgs);


    public DiagnosticInfo WithMessageArgs(params string[] args)
        => this with { MessageArgs = args };

    public DiagnosticInfo WithLocation(Location location)
        => this with { Location = location };
}