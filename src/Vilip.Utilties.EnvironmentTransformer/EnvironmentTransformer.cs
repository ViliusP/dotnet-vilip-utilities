using System.Collections;

namespace Vilip.Utilties.EnvironmentTransformer;


public class EnvironmentTransformer
{
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
