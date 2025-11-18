using Microsoft.Extensions.Configuration;

namespace Vilip.Utilities.Providers.DockerSecrets;


/// <summary>
/// Extensions for adding a file-backed environment variables source to an <see cref="IConfigurationBuilder"/>.
/// </summary>
public static class DockerSecretsProviderConfigurationExtensions
{
    extension(IConfigurationBuilder builder)
    {
        /// <summary>
        /// Adds a configuration source that augments environment variables with file-backed values.
        /// </summary>
        /// <param name="builder">The configuration builder.</param>
        /// <param name="configure">
        /// Options delegate to customize suffix, precedence, trimming, base path, encoding, failure handling,
        /// and the set of normalized keys that should be treated as file-backed without the suffix.
        /// </param>
        /// <returns>The configuration builder for chaining.</returns>
        /// <example>
        /// <code>
        /// builder.Configuration
        ///     .AddJsonFile("appsettings.json", optional: true)
        ///     .AddDockerSecrets(opt =>
        ///     {
        ///         opt.Suffix = "_FILE";
        ///         opt.TrimMode = FileTrimMode.TrailingNewlinesOnly;
        ///         opt.EnforceBasePath = true;
        ///         opt.BasePath = "/run/secrets";
        ///     })
        ///     .AddEnvironmentVariables("ASPNETCORE_");
        /// </code>
        /// </example>
        public IConfigurationBuilder AddDockerSecrets(Action<DockerSecretsProviderOptions>? configure = null)
        {
            var options = new DockerSecretsProviderOptions();
            configure?.Invoke(options);
            return builder.Add(new DockerSecretsSource(options));
        }
    }
}
