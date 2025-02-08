using Envied;

namespace dotnet8_example;

[Envied(path: ".env")]
public static class ExampleClass
{
    [EnviedField]
    public static string EXAMPLE_FIELD => ExampleClass_Generated.EXAMPLE_FIELD;

    [EnviedField(useConstantCase: true, optional: true)]
    public static string Optional_Field => ExampleClass_Generated.Optional_Field;

    [EnviedField(useConstantCase: true)]
    public static string Constant_Case_Field => ExampleClass_Generated.Constant_Case_Field;

    [EnviedField(obfuscate: true)]
    public static string OBFUSCATED_FIELD => ExampleClass_Generated.OBFUSCATED_FIELD;

    [EnviedField(randomSeed: 123)]
    public static string RANDOM_SEED_FIELD => ExampleClass_Generated.RANDOM_SEED_FIELD;

    [EnviedField]
    public static MyEnum ENUM_FIELD => ExampleClass_Generated.ENUM_FIELD;

    [EnviedField]
    public static DateTime DATE_TIME_FIELD => ExampleClass_Generated.DATE_TIME_FIELD;

    [EnviedField(optional:true, defaultValue: "True")]
    public static bool DEFAULT_VALUE_FIELD => ExampleClass_Generated.DEFAULT_VALUE_FIELD;

    [EnviedField]
    public static int INT_FIELD => ExampleClass_Generated.INT_FIELD;
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
        Console.WriteLine(ExampleClass.Optional_Field); // Should print empty string
        Console.WriteLine(ExampleClass.Constant_Case_Field); // Should print "constant_value"
        Console.WriteLine(ExampleClass.OBFUSCATED_FIELD); // Should print "obfuscated_value"
        Console.WriteLine(ExampleClass.RANDOM_SEED_FIELD); // Should print "random_seed_value"
        Console.WriteLine(ExampleClass.ENUM_FIELD); // Should print "Value1"
        Console.WriteLine(ExampleClass.DATE_TIME_FIELD); // Should print "2021-01-01T00:00:00"
        Console.WriteLine(ExampleClass.DEFAULT_VALUE_FIELD); // Should print "True"
    }
}