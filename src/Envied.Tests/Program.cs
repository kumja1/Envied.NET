namespace Envied.Tests;

public enum Color { Red, Green, Blue }


[Envied(obfuscate: false, useConstantCase: true, interpolate: true)]
static partial class Env
{

        [EnviedField]
        public static partial string BasicValue { get; }

        [EnviedField]
        public static partial string InterpolatedValue { get; }

        [EnviedField]
        public static partial Color ColorEnum { get; }

        [EnviedField]
        public static partial DateTime DateTime { get; }

}

static class Program
{
        public static void Main(string[] args)
        {
                Console.WriteLine(Env.ColorEnum);

        }
}
