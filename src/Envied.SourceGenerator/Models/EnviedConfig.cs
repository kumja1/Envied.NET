namespace Envied.SourceGenerator.Models;

public readonly record struct EnviedConfig(string Path, bool RequireEnvFile, string Name, bool Obfuscate, bool AllowOptionalFields, bool UseConstantCase, bool Interpolate, bool RawStrings, bool Environment, int RandomSeed);

public readonly record struct EnviedFieldConfig(string Name, bool UseConstantCase, bool Optional, bool Interpolate, bool RawString, bool Obfuscate, bool Environment, object? DefaultValue, int RandomSeed);

