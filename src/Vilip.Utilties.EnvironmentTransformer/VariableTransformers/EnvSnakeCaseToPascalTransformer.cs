using System.Globalization;

namespace Vilip.Utilties.EnvironmentTransformer.VariableTransformers;


/// <summary>
/// Transforms environment variable keys from (SCREAMING) snake_case (with optional double underscore sections)
/// into PascalCase segments, preserving section delimiters.
/// Example: "CLIENT__HOST_NAME" → "Client__HostName".
/// </summary>
public class EnvSnakeCaseToPascalTransformer : IVariableTransformer
{
    private readonly Matcher? _matcher;

    private readonly CultureInfo _cultureInfo;


    /// <summary>
    /// Initializes a new instance of <see cref="EnvSnakeCaseToPascalTransformer"/> that transforms all keys
    /// using <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public EnvSnakeCaseToPascalTransformer() : this(null, CultureInfo.InvariantCulture) { }

    /// <summary>
    /// Initializes a new instance of <see cref="EnvSnakeCaseToPascalTransformer"/> with a matcher.
    /// If <paramref name="matcher"/> is null, all keys are transformed.
    /// If <paramref name="matcher"/> returns true for a key, the key is transformed.
    /// If <paramref name="matcher"/> returns false, the original key is returned unchanged.
    /// </summary>
    /// <param name="matcher">Predicate to decide whether a key should be transformed.</param>
    public EnvSnakeCaseToPascalTransformer(Matcher? matcher) : this(matcher, CultureInfo.InvariantCulture) { }

    /// <summary>
    /// Initializes a new instance of <see cref="EnvSnakeCaseToPascalTransformer"/> with a matcher and culture.
    /// </summary>
    /// <param name="matcher">Predicate to decide whether a key should be transformed.</param>
    /// <param name="cultureInfo">Culture used for title casing. Defaults to <see cref="CultureInfo.InvariantCulture"/> if null.</param>
    public EnvSnakeCaseToPascalTransformer(Matcher? matcher, CultureInfo? cultureInfo)
    {
        _matcher = matcher;
        _cultureInfo = cultureInfo ?? CultureInfo.InvariantCulture;
    }


    /// <summary>
    /// Transforms the given environment variable key from snake_case to PascalCase.
    /// Sections separated by double underscores ("__") are preserved.
    /// Example: "CLIENT__HOST_NAME" → "Client__HostName".
    /// </summary>
    /// <param name="value">The environment variable key to transform.</param>
    /// <returns>The transformed key, or the original key if matcher returns false.</returns>
    public string Apply(string value)
    {
        if (_matcher is not null && !_matcher(value)) return value;


        var sections = value
            .Split(["__"], StringSplitOptions.None)
            .Select(SnakeCaseToPascal);

        return string.Join("__", sections);
    }

    /// <summary>
    /// Converts a single snake_case segment into PascalCase using the configured culture.
    /// Example: "mqtt_client" → "MqttClient".
    /// </summary>
    /// <param name="value">The segment to transform.</param>
    /// <returns>The PascalCase representation of the segment.</returns>
    private string SnakeCaseToPascal(string value)
    {
        var words = value
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => _cultureInfo.TextInfo.ToTitleCase(w.ToLower(_cultureInfo)));

        return string.Concat(words);
    }
}
