namespace Vilip.Utilties.EnvironmentTransformer;


public delegate string Transformer(string value);

public sealed class EnvironmentTransformerOptions
{
    /// <summary>
    /// Transforms key, assign value, and remove old key;
    /// </summary>
    public bool RemoveAfterTransform { get; set; } = false; 

    /// <summary>
    /// Transformers.add(x=>x.ToUpperCase())
    /// </summary>
    public required Transformer Transformer { get; set; }

    /// <summary>
    /// Optional scope for reading environment variables (Process/User/Machine). When unset, the runtime default is used.
    /// </summary>
    public EnvironmentVariableTarget? EnvironmentTarget { get; set; } // null => default
}
