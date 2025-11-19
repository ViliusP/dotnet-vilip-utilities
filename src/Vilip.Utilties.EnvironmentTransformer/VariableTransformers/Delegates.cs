namespace Vilip.Utilities.EnvironmentTransformer.VariableTransformers;


/// <summary>
/// Represents a predicate that determines whether a given environment variable key should be transformed.
/// </summary>
/// <param name="value">The environment variable key to evaluate.</param>
/// <returns>
/// <c>true</c> if the key should be transformed; otherwise, <c>false</c>.
/// </returns>
public delegate bool Matcher(string value);
