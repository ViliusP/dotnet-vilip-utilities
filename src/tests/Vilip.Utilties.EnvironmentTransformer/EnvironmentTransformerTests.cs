using FluentAssertions;

namespace Vilip.Utilties.EnvironmentTransformer.Tests;


public class EnvironmentTransformerTests : IDisposable
{
    private readonly List<(string key, EnvironmentVariableTarget target)> _cleanup = new();

    private void Set(string key, string? value, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
    {
        Environment.SetEnvironmentVariable(key, value, target);
        _cleanup.Add((key, target));
    }

    private string? Get(string key, EnvironmentVariableTarget target = EnvironmentVariableTarget.Process)
        => Environment.GetEnvironmentVariable(key, target);

    public void Dispose()
    {
        foreach (var (k, t) in _cleanup.Distinct())
        {
            Environment.SetEnvironmentVariable(k, null, t);
        }
        _cleanup.Clear();
    }

    [Fact]
    public void Apply_Renames_Key_And_Preserves_Old_When_RemoveAfterTransform_False()
    {
        // Arrange
        Set("MQTT_CLIENT__HOST", "broker");
        var options = new EnvironmentTransformerOptions
        {
            RemoveAfterTransform = false,
            Transformer = k => k.Replace("MQTT_CLIENT", "MqttClient")
        };

        // Act
        EnvironmentTransformer.Apply(options);

        // Assert
        Get("MqttClient__HOST").Should().Be("broker");
        Get("MQTT_CLIENT__HOST").Should().Be("broker", "old key should remain when RemoveAfterTransform=false");
    }

    [Fact]
    public void Apply_Renames_Key_And_Removes_Old_When_RemoveAfterTransform_True()
    {
        // Arrange
        Set("MQTT_CLIENT__HOST", "broker");
        var options = new EnvironmentTransformerOptions
        {
            RemoveAfterTransform = true,
            Transformer = k => k.Replace("MQTT_CLIENT", "MqttClient")
        };

        // Act
        EnvironmentTransformer.Apply(options);

        // Assert
        Get("MqttClient__HOST").Should().Be("broker");
        Get("MQTT_CLIENT__HOST").Should().BeNull("old key should be removed");
    }

    [Fact]
    public void Apply_NoOp_When_Transformer_Returns_Same_Key()
    {
        // Arrange
        Set("UNCHANGED_KEY", "value");
        var options = new EnvironmentTransformerOptions
        {
            RemoveAfterTransform = true,
            Transformer = k => k // identity
        };

        // Act
        EnvironmentTransformer.Apply(options);

        // Assert
        Get("UNCHANGED_KEY").Should().Be("value");
    }

    [Fact]
    public void Apply_Respects_Scope_Process()
    {
        // Arrange
        Set("SCOPED_KEY", "scoped_value", EnvironmentVariableTarget.Process);

        var options = new EnvironmentTransformerOptions
        {
            EnvironmentTarget = EnvironmentVariableTarget.Process,
            RemoveAfterTransform = true,
            Transformer = k => k == "SCOPED_KEY" ? "SCOPED_KEY_RENAMED" : k
        };

        // Act
        EnvironmentTransformer.Apply(options);

        // Assert (in Process scope)
        Get("SCOPED_KEY", EnvironmentVariableTarget.Process).Should().BeNull();
        Get("SCOPED_KEY_RENAMED", EnvironmentVariableTarget.Process).Should().Be("scoped_value");

        // And not leaking to User/Machine (cannot reliably assert Machine in CI, so we at least check null reads)
        Get("SCOPED_KEY_RENAMED", EnvironmentVariableTarget.User).Should().BeNull();
    }

    [Fact]
    public void Apply_MultipleKeys_All_Transformed()
    {
        // Arrange
        Set("A_ONE", "1");
        Set("B_TWO", "2");
        var options = new EnvironmentTransformerOptions
        {
            Transformer = k => k.StartsWith("A_") ? k.Replace("A_", "Alpha_") :
                               k.StartsWith("B_") ? k.Replace("B_", "Beta_") : k
        };

        // Act
        EnvironmentTransformer.Apply(options);

        // Assert
        Get("Alpha_ONE").Should().Be("1");
        Get("Beta_TWO").Should().Be("2");
    }

    [Fact]
    public void Apply_Skips_NonString_Values_Safely()
    {
        // This is hard to simulate directly (Environment variables are strings),
        // but we can at least assert that nothing blows up with normal strings.
        Set("NORMAL_KEY", "value");
        var options = new EnvironmentTransformerOptions
        {
            Transformer = k => "NEW_" + k
        };

        // Act
        var act = () => EnvironmentTransformer.Apply(options);

        // Assert
        act.Should().NotThrow();
        Get("NEW_NORMAL_KEY").Should().Be("value");
        // old remains (default RemoveAfterTransform=false)
        Get("NORMAL_KEY").Should().Be("value");
    }
}
