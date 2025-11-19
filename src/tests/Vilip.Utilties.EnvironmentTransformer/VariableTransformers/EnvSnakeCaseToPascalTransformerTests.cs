using System.Globalization;
using FluentAssertions;
using Vilip.Utilties.EnvironmentTransformer.VariableTransformers;
using Xunit;

namespace Vilip.Utilties.EnvironmentTransformer.Tests;

public class EnvSnakeCaseToPascalTransformerTests
{
    [Fact]
    public void NullMatcher_Transforms_All()
    {
        var t = new EnvSnakeCaseToPascalTransformer(matcher: null);

        t.Apply("CLIENT").Should().Be("Client");
        t.Apply("MQTT_CLIENT").Should().Be("MqttClient");
        t.Apply("very_long").Should().Be("VeryLong");
        t.Apply("CLIENT__HOST").Should().Be("Client__Host");
        t.Apply("CLIENT__MQTT_BROKER__HOST_NAME").Should().Be("Client__MqttBroker__HostName");
    }

    [Fact]
    public void MatcherTrue_Transforms()
    {
        // Matches everything that starts with CLIENT or MQTT
        static bool m(string v) =>
            v.StartsWith("CLIENT", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("MQTT", StringComparison.OrdinalIgnoreCase);

        var t = new EnvSnakeCaseToPascalTransformer(m);

        t.Apply("CLIENT__HOST").Should().Be("Client__Host");          // matched -> transformed
        t.Apply("MQTT_CLIENT__HOST_NAME").Should().Be("MqttClient__HostName"); // matched -> transformed
    }

    [Fact]
    public void MatcherFalse_Skips_And_Returns_Original()
    {
        // Only transform keys that start with MQTT_; others should be returned verbatim
        static bool m(string v) => v.StartsWith("MQTT_", StringComparison.OrdinalIgnoreCase);

        var t = new EnvSnakeCaseToPascalTransformer(m);

        // matched -> transform
        t.Apply("MQTT_CLIENT__HOST_NAME").Should().Be("MqttClient__HostName");

        // not matched -> skip (return original)
        t.Apply("CLIENT__HOST_NAME").Should().Be("CLIENT__HOST_NAME");
        t.Apply("SOME_OTHER").Should().Be("SOME_OTHER");
    }

    [Fact]
    public void Uses_CurrentCulture_TitleCase_For_Tokens()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var t = new EnvSnakeCaseToPascalTransformer(matcher: null);

            t.Apply("mqtt_client").Should().Be("MqttClient");
            t.Apply("api_url").Should().Be("ApiUrl");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
