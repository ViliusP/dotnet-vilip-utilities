namespace Vilip.Utilities.EnvironmentTransformer.VariableTransformers;


/// <summary>
/// Defines a contract for environment variable key transformers.
/// Implementations should provide logic to transform a given key into a new format.
/// </summary>
public interface IVariableTransformer
{
    /// <summary>
    /// Applies the transformation to the specified environment variable key.
    /// </summary>
    /// <param name="value">The environment variable key to transform.</param>
    /// <returns>The transformed key.</returns>
    public string Apply(string value);
}
