using System.Collections;

using Vilip.Utilties.EnvironmentTransformer;


class EnvironmentTransformer
{
    static void Apply(EnvironmentTransformerOptions options)
    {
        var env = options.EnvironmentTarget is EnvironmentVariableTarget target
            ? Environment.GetEnvironmentVariables(target)
            : Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry entry in env)
        {
            object objectKey = entry.Key;
            if (objectKey is not string key) continue;

            object? objectValue = entry.Value;
            if (objectValue == null || objectValue is not string value) continue;

            string newKey = options.Transformer(key);
            if(newKey == key) continue;

            Environment.SetEnvironmentVariable(newKey, value);
            if (options.RemoveAfterTransform)
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }
}