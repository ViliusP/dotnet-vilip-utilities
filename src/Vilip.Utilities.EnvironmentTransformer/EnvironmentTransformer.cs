using System.Collections;

namespace Vilip.Utilities.EnvironmentTransformer;


/// <summary>
/// Provides functionality to transform environment variable keys according to specified options.
/// </summary>
public class EnvironmentTransformer
{

    /// <summary>
    /// Applies the transformation defined in <see cref="EnvironmentTransformerOptions"/> to all environment variables
    /// in the specified scope (or the default scope if none is provided).
    /// </summary>
    /// <param name="options">
    /// The transformation options, including:
    /// <list type="bullet">
    /// <item><description><see cref="EnvironmentTransformerOptions.Transformer"/>: Delegate to transform keys.</description></item>
    /// <item><description><see cref="EnvironmentTransformerOptions.RemoveAfterTransform"/>: Whether to remove the original key after transformation.</description></item>
    /// <item><description><see cref="EnvironmentTransformerOptions.EnvironmentTarget"/>: Optional scope for environment variables.</description></item>
    /// </list>
    /// </param>
    /// <remarks>
    /// <para>
    /// Behavior:
    /// <list type="bullet">
    /// <item><description>If the transformed key is identical to the original key, no changes are made.</description></item>
    /// <item><description>If <see cref="EnvironmentTransformerOptions.RemoveAfterTransform"/> is true, the original key is deleted after transformation.</description></item>
    /// <item><description>Non-string keys or values are ignored.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void Apply(EnvironmentTransformerOptions options)
    {
        var env = options.EnvironmentTarget is EnvironmentVariableTarget target
            ? Environment.GetEnvironmentVariables(target)
            : Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry entry in env)
        {
            if (entry.Key is not string key) continue;
            if (entry.Value is not string value) continue;

            string newKey = options.Transformer(key);
            if (string.Equals(newKey, key, StringComparison.Ordinal)) continue;

            Environment.SetEnvironmentVariable(newKey, value, options.EnvironmentTarget ?? EnvironmentVariableTarget.Process);
            if (options.RemoveAfterTransform)
            {
                Environment.SetEnvironmentVariable(key, null, options.EnvironmentTarget ?? EnvironmentVariableTarget.Process);
            }
        }
    }
}
