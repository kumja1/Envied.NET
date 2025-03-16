namespace Envied.SourceGenerator.Models.Config;
using Envied.SourceGenerator.Extensions;

using Microsoft.CodeAnalysis.CSharp.Syntax;

public readonly record struct EnviedFieldConfig(string Name, bool UseConstantCase, bool Optional, bool Interpolate, bool RawString, bool Obfuscate, bool Environment, object? DefaultValue, int RandomSeed)
{
    public static EnviedFieldConfig From(AttributeSyntax attribute, EnviedConfig defaultConfig)
    {
        var name = attribute.GetArgument("Name", defaultConfig.Name);
        var useConstantCase = attribute.GetArgument("UseConstantCase", defaultConfig.UseConstantCase);
        var optional = attribute.GetArgument("Optional", defaultConfig.AllowOptionalFields);
        var interpolate = attribute.GetArgument("Interpolate", defaultConfig.Interpolate);
        var rawString = attribute.GetArgument("RawStrings", defaultConfig.RawStrings);
        var obfuscate = attribute.GetArgument("Obfuscate", defaultConfig.Obfuscate);
        var environment = attribute.GetArgument("Environment", defaultConfig.Environment);
        var defaultValue = attribute.GetArgument<object?>("DefaultValue");
        var randomSeed = attribute.GetArgument("RandomSeed", defaultConfig.RandomSeed);

        return new EnviedFieldConfig(name, useConstantCase, optional, interpolate, rawString, obfuscate, environment, defaultValue, randomSeed);
    }
}
