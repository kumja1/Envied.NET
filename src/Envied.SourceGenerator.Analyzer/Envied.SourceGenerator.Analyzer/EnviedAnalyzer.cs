using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Envied.SourceGenerator.Common.Models.Config;
using Envied.SourceGenerator.Common.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Envied.SourceGenerator.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class EnviedAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENV";

    private static readonly Regex _interpolationRegex = new(
        @"\$\{([^}]+)\}",
        RegexOptions.Compiled
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        DiagnosticMessages.SupportedDiagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.Attribute);
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classAttribute = (AttributeSyntax)context.Node;
        var supportsPartial = ProjectHelper.CheckSupportsPartial(
            context.Options.AnalyzerConfigOptionsProvider,
            context.CancellationToken
        );
        if (classAttribute.Name.ToString() != "Envied")
            return;

        var classDecl = classAttribute.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDecl == null)
            return;

        if (!classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            if (!classDecl.Modifiers.Any(SyntaxKind.PartialKeyword) && supportsPartial)
            {
                ReportDiagnostic(
                    context,
                    DiagnosticMessages.ClassMustBePartial,
                    classAttribute.GetLocation()
                );
            }

            ReportDiagnostic(
                context,
                DiagnosticMessages.ClassMustBeStatic,
                classAttribute.GetLocation()
            );
            return;
        }

        var projectRoot = Path.GetDirectoryName(classDecl.SyntaxTree.FilePath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            ReportDiagnostic(
                context,
                DiagnosticMessages.ProjectRootNotFound,
                classAttribute.GetLocation()
            );
            return;
        }

        var config = EnviedConfig.From(classAttribute);
        var envPath = Path.Combine(projectRoot, config.Path);

        var env = EnviromentHelper.LoadEnvironment(envPath);
        if (env == null && config.RequireEnvFile)
        {
            ReportDiagnostic(
                context,
                DiagnosticMessages.EnvFileNotFound,
                classAttribute.GetLocation()
            );
            return;
        }

        foreach (var member in classDecl.Members)
        {
            if (member is not PropertyDeclarationSyntax property)
                continue;

            var propertyAttribute = property
                .AttributeLists.SelectMany(x => x.Attributes)
                .FirstOrDefault(x => x.Name.ToString() == "EnviedField");

            if (propertyAttribute == null)
                continue;

            AnalyzeMember(
                context,
                config,
                property,
                propertyAttribute,
                env,
                supportsPartial
            );
        }
    }

    private static void AnalyzeMember(
        SyntaxNodeAnalysisContext context,
        EnviedConfig config,
        PropertyDeclarationSyntax property,
        AttributeSyntax propertyAttribute,
        Dictionary<string, string>? env,
        bool supportsPartial
    )
    {
        if (!property.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            if (!property.Modifiers.Any(SyntaxKind.PartialKeyword) && supportsPartial)
            {
                ReportDiagnostic(
                    context,
                    DiagnosticMessages.PropertyMustBePartial,
                    property.GetLocation()
                );
            }

            ReportDiagnostic(
                context,
                DiagnosticMessages.PropertyMustBePartial,
                property.GetLocation()
            );
            return;
        }

        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
        if (propertySymbol == null)
            return;

        var propertyConfig = EnviedFieldConfig.From(propertyAttribute, config);
        if (propertyConfig == null)
           return;

        var envName = property.Identifier.Text;
        if (propertyConfig.UseConstantCase)
            envName = envName.ToUpper();

        if (env?.ContainsKey(envName) == false && !propertyConfig.Optional && propertyConfig.DefaultValue == null)
        {
            ReportDiagnostic(
                context,
                DiagnosticMessages.EnvVariableNotFound,
                property.GetLocation(),
                envName
            );
            return;
        }
        
        var typeInfo = context.SemanticModel.GetTypeInfo(property.Type);
        if (
            propertySymbol.Type.IsValueType
            && propertyConfig.Optional
            && !typeInfo.Nullability.Annotation.HasFlag(NullableAnnotation.Annotated)
        )
        {
            ReportDiagnostic(
                context,
                DiagnosticMessages.OptionalValueTypesMustBeNullable,
                property.GetLocation()
            );
            return;
        }

        string value = string.Empty;
        _ = env?.TryGetValue(envName, out value);
        if (!TypeHelper.IsValidTypeConversion(value, propertySymbol.Type))
        {
            ReportDiagnostic(
                context,
                DiagnosticMessages.InvalidTypeConversion,
                property.GetLocation(),
                value,
                propertySymbol.Type.Name
            );
            return;
        }

        if (propertyConfig.Interpolate && !string.IsNullOrEmpty(value))
        {
            var match = _interpolationRegex.Match(value);
            if (match.Success)
            {
                var interpolateEnvVar = match.Groups[1].Value;
                if (env?.ContainsKey(interpolateEnvVar) == false)
                {
                    ReportDiagnostic(
                        context,
                        DiagnosticMessages.EnvVariableNotFound,
                        property.GetLocation(),
                        interpolateEnvVar
                    );
                    return;
                }
            }
        }
    }

    private static void ReportDiagnostic(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor diagnostic,
        Location location,
        params object[] messageArgs
    ) => context.ReportDiagnostic(Diagnostic.Create(diagnostic, location, messageArgs));
}
