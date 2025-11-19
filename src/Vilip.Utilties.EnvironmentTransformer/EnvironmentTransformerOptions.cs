namespace Vilip.Utilties.EnvironmentTransformer;


/// <summary>
/// Represents a delegate that transforms an environment variable key into a new format.
/// </summary>
/// <param name="value">The original environment variable key.</param>
/// <returns>The transformed key.</returns>
public delegate string Transformer(string value);


/// <summary>
/// Options for configuring environment variable transformation behavior.
/// </summary>
public sealed class EnvironmentTransformerOptions
{

    /// <summary>
    /// Gets or sets a value indicating whether the original environment variable should be removed
    /// after its key has been transformed and reassigned.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, the original key will be deleted after transformation.
    /// When <c>false</c>, both the original and transformed keys will exist.
    /// </remarks>
    public bool RemoveAfterTransform { get; set; } = false;

    /// <summary>
    /// Gets or sets the transformer delegate used to transform environment variable keys.
    /// Example usage: <c>options.Transformer = key => key.ToUpperInvariant();</c>
    /// </summary>
    /// <remarks>
    /// This property is required. If not set, the transformation cannot be applied.
    /// </remarks>
    public required Transformer Transformer { get; set; }

    /// <summary>
    /// Gets or sets the optional scope for reading and writing environment variables.
    /// When unset (<c>null</c>), the runtime default scope is used.
    /// </summary>
    /// <remarks>
    /// Possible values:
    /// <list type="bullet">
    /// <item><description><see cref="EnvironmentVariableTarget.Process"/> - Current process only.</description></item>
    /// <item><description><see cref="EnvironmentVariableTarget.User"/> - User-level environment variables.</description></item>
    /// <item><description><see cref="EnvironmentVariableTarget.Machine"/> - Machine-level environment variables.</description></item>
    /// </list>
    /// </remarks>
    public EnvironmentVariableTarget? EnvironmentTarget { get; set; } // null => default
}
