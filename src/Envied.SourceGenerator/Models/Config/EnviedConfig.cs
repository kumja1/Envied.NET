using Microsoft.CodeAnalysis.CSharp.Syntax;
using Envied.SourceGenerator.Extensions;

namespace Envied.SourceGenerator.Models.Config;

internal readonly record struct EnviedConfig(string Path, bool RequireEnvFile, string Name, bool Obfuscate, bool AllowOptionalFields, bool UseConstantCase, bool Interpolate, bool RawStrings, bool Environment, int RandomSeed)
{
    public static EnviedConfig From(AttributeSyntax attribute)
    {
        var path = attribute.GetArgument<string>("Path",".env");
        var requireEnvFile = attribute.GetArgument<bool>("RequireEnvFile");
        var name = attribute.GetArgument<string>("Name","");
        var obfuscate = attribute.GetArgument<bool>("Obfuscate");
        var allowOptionalFields = attribute.GetArgument<bool>("AllowOptionalFields");
        var useConstantCase = attribute.GetArgument<bool>("UseConstantCase");
        var interpolate = attribute.GetArgument<bool>("Interpolate");
        var rawStrings = attribute.GetArgument<bool>("RawStrings");
        var environment = attribute.GetArgument<bool>("Environment");
        var randomSeed = attribute.GetArgument<int>("RandomSeed");

        return new EnviedConfig(path, requireEnvFile, name, obfuscate, allowOptionalFields, useConstantCase, interpolate, rawStrings, environment, randomSeed);
    }
}

