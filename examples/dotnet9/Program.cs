using Envied;

namespace dotnet9_example;

[Envied]
public static partial class ExampleClass
{
    [EnviedField]

    public static partial string EXAMPLE_FIELD { get; }

    [EnviedField(optional: true)]
    public static partial string OPTIONAL_FIELD { get; }

    [EnviedField(useConstantCase: true)]
    public static partial string Constant_Case_Field { get; }

    [EnviedField(obfuscate: true)]
    public static partial string OBFUSCATED_FIELD { get; }

    [EnviedField(randomSeed: 123, obfuscate:true)]
    public static partial string RANDOM_SEED_FIELD { get; }

    [EnviedField]
    public static partial MyEnum ENUM_FIELD { get; }

    [EnviedField]
    public static partial DateTime DATE_TIME_FIELD { get; }

    [EnviedField(defaultValue: true)]
    public static partial bool DEFAULT_VALUE_Field { get; }

    [EnviedField(rawString: true)]
    public static partial string RAW_STRING_FIELD { get; }

    [EnviedField(interpolate: true)]
    public static partial string INTERPOLATION_FIELD { get; }
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
        Console.WriteLine(ExampleClass.EXAMPLE_FIELD);
        Console.WriteLine(ExampleClass.OPTIONAL_FIELD);
        Console.WriteLine(ExampleClass.Constant_Case_Field);
        Console.WriteLine(ExampleClass.OBFUSCATED_FIELD);
        Console.WriteLine(ExampleClass.RANDOM_SEED_FIELD);
        Console.WriteLine(ExampleClass.ENUM_FIELD);
        Console.WriteLine(ExampleClass.DATE_TIME_FIELD);
        Console.WriteLine(ExampleClass.DEFAULT_VALUE_Field);
        Console.WriteLine(ExampleClass.INTERPOLATION_FIELD);
        Console.WriteLine(ExampleClass.RAW_STRING_FIELD);

    }
}