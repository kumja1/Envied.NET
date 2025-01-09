namespace Envied;

[AttributeUsage(AttributeTargets.Field)]
public class EnviedField(
    string varName = null,
    bool? obfuscate = null,
    object defaultValue = null,
    bool? environment = null,
    bool? optional = null,
    bool? useConstantCase = null,
    bool? interpolate = null,
    bool? rawString = null,
    int? randomSeed = null) : Attribute
{
    /// <summary>
    /// The environment variable name specified in the `.env` file to generate for the annotated variable
    /// </summary>
    public string VarName { get; } = varName;

    /// <summary>
    /// Allows this values to be encrypted using a random
    /// generated key that is then XOR'd with the encrypted
    /// value when being accessed the first time.
    /// Please note that the values can not be offered with
    /// the `const` qualifier, but only with `final`.
    /// **Overrides the per-class obfuscate option!**
    /// </summary>
    public bool? Obfuscate { get; } = obfuscate;

    /// <summary>
    /// Allows this default value to be used if the environment variable is not set.
    /// The default value to use if the environment variable
    /// is not specified in the `.env` file.
    /// The default value not to use if the environment variable
    /// is specified in the `.env` file.
    /// The default value must be a [String], [bool] or a [num].
    /// </summary>
    public object DefaultValue { get; } = defaultValue;

    /// <summary>
    /// When set to `true`, the value set in the `.env` file will not be used as
    /// the ultimate value but will instead be used as the key and the ultimate
    /// value will be read from [Platform.environment].
    /// </summary>
    public bool? Environment { get; } = environment;

    /// <summary>
    /// Allows this field to be optional when the type is nullable.
    ///
    /// With this enabled, the generator will not throw an exception
    /// if the environment variable is missing and a default value was
    /// not set.
    /// </summary>
    public bool? Optional { get; } = optional;

    /// <summary>
    /// Whether to convert the field name to CONSTANT_CASE.
    ///
    /// By default, this is set to `false`, which means that the field name will
    /// retain its original format unless [varName] is specified.
    ///
    /// When set to `true`, the field name will be automatically transformed
    /// into CONSTANT_CASE. This follows the Dart convention for constant
    /// names where all letters are capitalized, and words are separated by
    /// underscores.
    /// </summary>
    public bool? UseConstantCase { get; } = useConstantCase;

    /// <summary>
    /// Whether to use the interpolated value for the field.
    /// If [interpolate] is `true`, the value will be interpolated
    /// with the environment variables.
    /// </summary>
    public bool? Interpolate { get; } = interpolate;

    /// <summary>
    /// Whether to use the raw string format for the value.
    ///
    /// Can only be used with a [String] type.
    ///
    /// **NOTE**: The string is always formatted `'<value>'`.
    ///
    /// If [rawString] is `true`, creates a raw String formatted `r'<value>'`
    /// and the value may not contain a single quote.
    /// Escapes single quotes and newlines in the value.
    /// </summary>
    public bool? RawString { get; } = rawString;

    /// <summary>
    /// A seed can be provided if the obfuscation randomness needs to remain
    /// reproducible across builds.
    /// **Note**: This will make the `Random` instance non-secure!
    /// </summary>
    public int? RandomSeed { get; } = randomSeed;
}


[Envied()]
class Env {

}