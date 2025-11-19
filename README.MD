# Utilities for .NET

TODO

## Vilip.Utilties.EnvironmentTransformer

`Vilip.Utilties.EnvironmentTransformer` is a lightweight .NET utility for transforming environment variable keys and values. It provides:

- **Custom key transformers** via delegates.
- **Flexible options** for removing original keys after transformation.
- **Support for hierarchical keys** using double underscores (`__`).
- **Culture-aware transformations** for snake_case to PascalCase.

This library is ideal for applications that need to normalize environment variable names into configuration-friendly formats.

### Recommended Uses

- **Normalize Environment Variables for Configuration**
  - Convert snake_case or uppercase environment variable names into PascalCase keys for use with `Microsoft.Extensions.Configuration`.
  - Example: `MQTT_CLIENT__HOST` → `MqttClient:Host`.

- **Enforce Consistent Naming Conventions**
  - Apply culture-invariant casing rules to avoid locale-specific issues in CI/CD pipelines or containerized environments.

- **Conditional Transformation**
  - Use `Matcher` to selectively transform keys based on patterns (e.g., only transform keys starting with `MQTT_`).

- **Integration with Docker/Kubernetes**
  - Normalize environment variables injected by orchestrators into predictable formats for .NET configuration systems.

- **Migration from Legacy Systems**
  - Transform legacy environment variable names to modern naming conventions without breaking existing logic.

- **Security and Cleanup**
  - Remove original keys after transformation to prevent accidental use of outdated names (`RemoveAfterTransform = true`).

### Example - Basic Usage

```csharp
using Vilip.Utilties.EnvironmentTransformer;

// Configure options
var options = new EnvironmentTransformerOptions
{
    Transformer = key => key.ToUpperInvariant(), // Example: transform to uppercase
    RemoveAfterTransform = true // Remove original keys after transformation
};

// Apply transformation
EnvironmentTransformer.Apply(options);
```

### Example - Using a Custom Transformer (SnakeCase → PascalCase)

```csharp
using Vilip.Utilties.EnvironmentTransformer.VariableTransformers;

// Create transformer
var transformer = new EnvSnakeCaseToPascalTransformer();

// Example input: "MQTT_CLIENT__HOST_NAME"
// Output: "MqttClient__HostName"
string transformed = transformer.Apply("MQTT_CLIENT__HOST_NAME");
```

### Example - Conditional Transformation with Matcher

```csharp
using Vilip.Utilties.EnvironmentTransformer.VariableTransformers;

// Transform only keys starting with "MQTT_"
Matcher matcher = key => key.StartsWith("MQTT_", StringComparison.OrdinalIgnoreCase);
var transformer = new EnvSnakeCaseToPascalTransformer(matcher);

string result = transformer.Apply("MQTT_CLIENT__HOST"); // => "MqttClient__Host"
string skipped = transformer.Apply("OTHER_KEY");        // => "OTHER_KEY"
```
