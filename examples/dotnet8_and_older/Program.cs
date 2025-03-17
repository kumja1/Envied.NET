using Envied;


namespace dotnet8_and_older;

[Envied(path: ".env")]
public static class ExampleClass
{
    [EnviedField] public static string EXAMPLE_FIELD => ExampleClass_Generated.EXAMPLE_FIELD;

    [EnviedField(optional: true)] public static string OPTIONAL_FIELD => ExampleClass_Generated.OPTIONAL_FIELD;

    [EnviedField(useConstantCase: true)]
    public static string CONSTANT_CASE_FIELD => ExampleClass_Generated.CONSTANT_CASE_FIELD;

    [EnviedField(obfuscate: true)]
    public static string OBFUSCATED_FIELD => ExampleClass_Generated.OBFUSCATED_FIELD;

    [EnviedField(randomSeed: 123, obfuscate: true)]
    public static string RANDOM_SEED_FIELD => ExampleClass_Generated.RANDOM_SEED_FIELD;

    [EnviedField]
    public static MyEnum ENUM_FIELD => ExampleClass_Generated.ENUM_FIELD;

    [EnviedField]
    public static DateTime DATE_TIME_FIELD => ExampleClass_Generated.DATE_TIME_FIELD;

    [EnviedField(defaultValue: true)]
    public static bool DEFAULT_VALUE_FIELD => ExampleClass_Generated.DEFAULT_VALUE_FIELD;

    [EnviedField(rawString: true)]
    public static string RAW_STRING_FIELD => ExampleClass_Generated.RAW_STRING_FIELD;

    [EnviedField(interpolate: true)]
    public static string INTERPOLATION_FIELD => ExampleClass_Generated.INTERPOLATION_FIELD;
}

public enum MyEnum
{
    Value1,
    Value2,
    Value3
}

public static class Program
{
    static void Main()
    {
        Console.WriteLine(ExampleClass.EXAMPLE_FIELD); // Should print "example_value"
        Console.WriteLine(ExampleClass.OPTIONAL_FIELD); // Should print empty string
        Console.WriteLine(ExampleClass.CONSTANT_CASE_FIELD); // Should print "constant_value"
        Console.WriteLine(ExampleClass.OBFUSCATED_FIELD); // Should print "obfuscated_value"
        Console.WriteLine(ExampleClass.RANDOM_SEED_FIELD); // Should print "random_seed_value"
        Console.WriteLine(ExampleClass.ENUM_FIELD); // Should print "Value1"
        Console.WriteLine(ExampleClass.DATE_TIME_FIELD); // Should print "2021-01-01T00:00:00"
        Console.WriteLine(ExampleClass.DEFAULT_VALUE_FIELD); // Should print "True"
        Console.WriteLine(ExampleClass.RAW_STRING_FIELD); // Should print raw string value
        Console.WriteLine(ExampleClass.INTERPOLATION_FIELD); // Should print interpolated string value
    }
}