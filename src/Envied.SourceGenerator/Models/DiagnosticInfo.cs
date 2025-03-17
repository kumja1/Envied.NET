using Microsoft.CodeAnalysis;

namespace Envied.SourceGenerator.Models;

internal record DiagnosticInfo(
    string Id,
    string Title, 
    string Message,
    DiagnosticSeverity Severity,
    LocationInfo Location = null,
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
        Location.ToLocation(),  
        MessageArgs);


    public DiagnosticInfo WithMessageArgs(params string[] args)
        => this with { MessageArgs = args };

    public DiagnosticInfo WithLocation(Location location)
        => WithLocation(LocationInfo.From(location));

     public DiagnosticInfo WithLocation(LocationInfo location)
        => this with { Location = location };
}