using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Envied.SourceGenerator.Tests;

public class EnviedSourceGeneratorTests
{
    [Fact]
    public void Generator_ShouldNotThrowErrors()
    {
        var inputCode = """
        using Envied;
        namespace dotnet9_example;

        [Envied]
        public static partial class ExampleClass
        {
            [EnviedField(defaultValue: "default_value")]
            public static partial string EXAMPLE_FIELD { get; }

            [EnviedField(optional: true, defaultValue: "optional_value")]
            public static partial string OPTIONAL_FIELD { get; }

            [EnviedField(useConstantCase: true, defaultValue: "constant_case_value")]
            public static partial string Constant_Case_Field { get; }

            [EnviedField(obfuscate: true, defaultValue: "obfuscated_value")]
            public static partial string OBFUSCATED_FIELD { get; }

            [EnviedField(randomSeed: 123, obfuscate: true, defaultValue: "random_seed_value")]
            public static partial string RANDOM_SEED_FIELD { get; }

            [EnviedField(defaultValue: "2023-01-01T00:00:00Z")]
            public static partial DateTime DATE_TIME_FIELD { get; }

            [EnviedField(defaultValue: true)]
            public static partial bool DEFAULT_VALUE_Field { get; }

            [EnviedField(rawString: true, defaultValue: "raw_string_value")]
            public static partial string RAW_STRING_FIELD { get; }

            [EnviedField(interpolate: true, defaultValue: "interpolation_value")]
            public static partial string INTERPOLATION_FIELD { get; }
        }
        """;

        var syntaxTree = CSharpSyntaxTree.ParseText(inputCode);
        var compilation = CSharpCompilation.Create("TestCompilation")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(EnviedAttribute).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var generator = new EnviedSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        Console.WriteLine(string.Join('\n', outputCompilation.SyntaxTrees.Select(s => s.ToString())));

        // Log errors if there are any
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors); // Fails the test if errors are found
    }
}
