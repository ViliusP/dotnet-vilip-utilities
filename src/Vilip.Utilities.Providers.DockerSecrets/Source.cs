using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Vilip.Utilities.Providers.DockerSecrets;


/// <summary>
/// An <see cref="IConfigurationSource"/> that builds a <see cref="DockerSecretsProvider"/>.
/// </summary>
/// <remarks>
///     <para><strong>Behavior</strong></para>
///     <list type="bullet">
///         <item>
///             <description>
///                 Suffix handling: variables ending with <see cref="DockerSecretsProviderOptions.Suffix"/> (e.g., <c>_FILE</c>)
///                 are interpreted as file paths; the file content is stored under the base key (suffix removed).
///                 The <c>*_FILE</c> key itself is not emitted.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Allow-list handling: keys listed in <see cref="DockerSecretsProviderOptions.AdditionalFileBackedKeys"/>
///                 are treated as file paths even without the suffix; their values are replaced by file content.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Prefix filtering: when <see cref="DockerSecretsProviderOptions.Prefix"/> is set, only variables with that
///                 prefix are considered; the prefix is removed before key normalization.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Normalization: double underscore <c>__</c> becomes section separator <c>:</c> (e.g., <c>A__B__C</c> → <c>A:B:C</c>).
///             </description>
///         </item>
///         <item>
///             <description>
///                 Precedence: by default, file content overrides plain env values
///                 (<see cref="DockerSecretsProviderOptions.PreferFileContent"/>). Per-key overrides can be supplied via
///                 <see cref="DockerSecretsProviderOptions.PreferFileContentKeys"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Trimming: file content is trimmed per <see cref="DockerSecretsProviderOptions.TrimMode"/>.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Safety: when <see cref="DockerSecretsProviderOptions.EnforceBasePath"/> is <see langword="true"/>,
///                 the resolved file path must lie under <see cref="DockerSecretsProviderOptions.BasePath"/>; and files larger
///                 than <see cref="DockerSecretsProviderOptions.MaxFileBytes"/> are rejected. Such cases are reported via
///                 <see cref="DockerSecretsProviderOptions.OnFail"/> and skipped.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Failures: missing/denied/I/O issues do not throw. They are reported via
///                 <see cref="DockerSecretsProviderOptions.OnFail"/> and the corresponding value is skipped.
///             </description>
///         </item>
///     </list>
///     <para>
///         Empty files are valid and set the configuration value to <c>""</c>.
///     </para>
/// </remarks>
public sealed class DockerSecretsSource : IConfigurationSource
{
    /// <summary>
    /// The options that govern how file-backed environment variables are interpreted.
    /// </summary>
    public DockerSecretsProviderOptions Options { get; }
    private readonly IFileSystem? _fs;

    /// <summary>
    /// Creates the source with the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Behavioral options for the provider.</param>
    public DockerSecretsSource(DockerSecretsProviderOptions options, IFileSystem? fs = null)
    {
        Options = options;
        _fs = fs;
    }

    /// <summary>
    /// Builds the <see cref="IConfigurationProvider"/> responsible for loading values.
    /// </summary>
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new DockerSecretsProvider(Options, _fs);
}
