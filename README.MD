# Utilities for .NET

## Vilip.Utilities.Providers.DockerSecrets

`DockerSecretsProvider` is a custom configuration provider for .NET applications that integrates Docker secrets into the `Microsoft.Extensions.Configuration` system. It reads environment variables and resolves file-backed values (e.g., `*_FILE` patterns) commonly used in containerized environments.

Key features:

- **File-backed secrets**: Automatically loads secret values from files referenced by environment variables.
- **Prefix and suffix filtering**: Supports filtering keys by prefix and suffix for better control.
- **Key normalization**: Converts `__` to `:` to match .NET hierarchical configuration conventions.
- **Safe error handling**: Reports file-read failures via callbacks without throwing exceptions.
- **Caching**: Avoids redundant file reads for performance.

### Why Use This?

Why Use This?

- Security: Avoid exposing sensitive values in plain environment variables.
- Compatibility: Works seamlessly with Docker and Kubernetes secrets.
- Flexibility: Supports prefix filtering, suffix handling, and allow-listed keys.
- Performance: Caches file reads to minimize I/O overhead.

### Example Environment Variables

```env
MYAPP_DB_PASSWORD_FILE=/run/secrets/db_password
MYAPP_DB_HOST=database.local
```

Resulting configuration keys:

```csharp
configuration["Db:Password"] // Loaded from file content
configuration["Db:Host"]     // Loaded from plain environment variable
```

### Example - Basic Integration in ASP.NET Core

Add the provider to your configuration pipeline:

```csharp
using Vilip.Utilities.Providers.DockerSecrets;

var builder = WebApplication.CreateBuilder(args);

// Register Docker secrets provider with default options
builder.Configuration.AddDockerSecrets();
```

This will:

- Normalize keys (`__`→`:`).
- Handle `*_FILE` suffix for file-backed secrets.
- Prefer file content over plain environment values by default.

### Example - Custom Options

Configure advanced behavior:

```csharp
builder.Configuration.AddDockerSecrets(options =>
{
    options.Prefix = "MYAPP_"; // Only process keys starting with MYAPP_
    options.Suffix = "_FILE";  // Treat *_FILE as file-backed secrets
    options.PreferFileContent = true; // File content wins over plain env values
    options.TrimMode = FileTrimMode.TrailingWhitespace; // Trim trailing whitespace/newlines
    options.BasePath = "/run/secrets"; // Restrict file reads to Docker secrets directory
    options.EnforceBasePath = true; // Prevent reading files outside BasePath
    options.MaxFileBytes = 64 * 1024; // Limit file size to 64 KiB
    options.AdditionalFileBackedKeys = new HashSet<string>
    {
        "ConnectionStrings:DbPassword",
        "Kestrel:Certificates:Default:Password"
    };
    options.OnFail = ex => Console.Error.WriteLine($"[DockerSecrets] {ex.Message}");
});
```

## Vilip.Utilties.EnvironmentTransformer

`Vilip.Utilties.EnvironmentTransformer` is a lightweight .NET utility for transforming environment variable keys and values. 

It provides:

- **Custom key transformers** via delegates.
- **Flexible options** for removing original keys after transformation.
- **Support for hierarchical keys** using double underscores (`__`).
- **Culture-aware transformations** for snake_case to PascalCase.

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
