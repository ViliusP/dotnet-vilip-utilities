using System.Globalization;
using FluentAssertions;
using Vilip.Utilities.EnvironmentTransformer.VariableTransformers;

namespace Vilip.Utilities.EnvironmentTransformer.Tests.VariableTransformers;


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

    [Fact]
    public void EmptyString_Returns_Empty()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("").Should().Be("");
    }

    [Fact]
    public void SingleUnderscore_Treated_As_Separator()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("_leading").Should().Be("Leading");
        t.Apply("trailing_").Should().Be("Trailing");
        t.Apply("_both_").Should().Be("Both");
    }

    [Fact]
    public void MultipleConsecutiveUnderscores_Are_Ignored()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("mqtt__client").Should().Be("Mqtt__Client");
        t.Apply("mqtt___client").Should().Be("Mqtt__Client");
    }

    [Fact]
    public void DoubleUnderscore_Splits_Sections_Correctly()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("mqtt__client__host").Should().Be("Mqtt__Client__Host");
    }

    [Fact]
    public void MixedCase_Input_Normalized_To_Pascal()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("mQtT_cLiEnT").Should().Be("MqttClient");
    }

    [Fact]
    public void Numbers_Are_Preserved()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("mqtt_client_123").Should().Be("MqttClient123");
    }

    [Fact]
    public void SpecialCharacters_Remain_In_Words()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null);
        t.Apply("mqtt_client_v2_beta").Should().Be("MqttClientV2Beta");
    }

    [Fact]
    public void MatcherTrue_On_EmptyString_Transforms()
    {
        static bool m(string v) => string.IsNullOrEmpty(v);
        var t = new EnvSnakeCaseToPascalTransformer(m);
        t.Apply("").Should().Be(""); // Nothing to transform, but logic still runs
    }

    [Fact]
    public void MatcherFalse_On_All_Skips_All()
    {
        static bool m(string v) => false;
        var t = new EnvSnakeCaseToPascalTransformer(m);
        t.Apply("mqtt_client").Should().Be("mqtt_client");
    }

    [Fact]
    public void Invariant_TitleCase_Is_Stable_Across_Cultures()
    {
        var t = new EnvSnakeCaseToPascalTransformer(null, new CultureInfo("tr-TR"));
        t.Apply("istanbul_api").Should().Be("İstanbulApi");
    }
}
