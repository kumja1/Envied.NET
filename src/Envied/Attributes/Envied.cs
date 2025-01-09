namespace Envied;

[AttributeUsage(AttributeTargets.Class)]
public class Envied(
    string path = ".env",
    bool requireEnvFile = false,
    string name = null,
    bool obfuscate = false,
    bool allowOptionalFields = false,
    bool useConstantCase = false,
    bool environment = false,
    bool interpolate = true,
    bool rawStrings = false,
    int? randomSeed = null) : Attribute
{
    /// <summary>
    /// The file path of the `.env` file, relative to the project root, which
    /// will be used to generate environment variables.
    ///
    /// If `null` or an empty [String], `.env` is used.
    /// </summary>
    public string Path { get; set; } = path;

    /// <summary>
    /// Whether to require a env file exists, or else the build_runner will fail if the file does not exits
    /// </summary>
    public bool RequireEnvFile { get; set; } = requireEnvFile;

    /// <summary>
    /// The value to use as name for the generated class, with
    /// an underscore `_` prefixed.
    ///
    /// If `null` or an empty [String], the name of the annotated class is used.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Allows all the values to be encrypted using a random
    /// generated key that is then XOR'd with the encrypted
    /// value when being accessed the first time.
    /// Please note that the values can not be offered with
    /// the `const` qualifier, but only with `final`.
    /// **Can be overridden by the per-field obfuscate option!**
    /// </summary>
    public bool Obfuscate { get; set; } = obfuscate;

    /// <summary>
    /// Allows all the values to be optional when the type is nullable.
    ///
    /// With this enabled, the generator will not throw an exception
    /// if the environment variable is missing and a default value was
    /// not set.
    /// </summary>
    public bool AllowOptionalFields { get; set; } = allowOptionalFields;

    /// <summary>
    /// Whether to convert field names from camelCase to CONSTANT_CASE when
    /// the @EnvField annotation is not explicitly assigned a varName.
    ///
    /// By default, this is set to `false`, which means field names will
    /// retain their original camelCase format unless varName is specified.
    /// </summary>
    public bool UseConstantCase { get; set; } = useConstantCase;

    /// <summary>
    /// Whether to read the ultimate values from [Platform.environment] rather
    /// than from the `.env` file.  When set to true, the value found in the
    /// `.env` file will not be used as the ultimate value but will instead be
    /// used as the key and the ultimate value will be read from
    /// [Platform.environment].
    /// </summary>
    public bool Environment { get; set; } = environment;

    /// <summary>
    /// Whether to interpolate the values for all fields.
    /// If [interpolate] is `true`, the value will be interpolated
    /// with the environment variables.
    /// </summary>
    public bool Interpolate { get; set; } = interpolate;

    /// <summary>
    /// Whether to use the raw string format for all string values.
    ///
    /// **NOTE**: The string is always formatted `'<value>'`.
    ///
    /// If [rawStrings] is `true`, all Strings will be raw formatted `r'<value>'`
    /// and the value may not contain a single quote.
    /// Escapes single quotes and newlines in the value.
    /// </summary>
    public bool RawStrings { get; set; } = rawStrings;

    /// <summary>
    /// A seed can be provided if the obfuscation randomness needs to remain
    /// reproducible across builds.
    /// **Note**: This will make the `Random` instance non-secure!
    /// </summary>
    public int? RandomSeed { get; set; } = randomSeed;
}