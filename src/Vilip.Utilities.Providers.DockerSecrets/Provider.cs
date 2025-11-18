

using System.Collections;
using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Vilip.Utilities.Providers.DockerSecrets.Helpers;

namespace Vilip.Utilities.Providers.DockerSecrets;


/// <summary>
/// A configuration provider that augments environment variables with file-backed values via a suffix and/or an allow-list of keys.
/// </summary>
/// <remarks>
/// <para>
/// Behavior:
/// </para>
/// <list type="bullet">
///     <item><description>
///         For variables ending with <see cref="DockerSecretsProviderOptions.Suffix"/> (e.g., <c>_FILE</c>), the value is treated as a file path.
///         The provider reads the file and stores its content under the base key (suffix removed). The <c>*_FILE</c> key itself is not added to configuration.
///     </description></item>
///     <item><description>
///         For keys listed in <see cref="DockerSecretsProviderOptions.AdditionalFileBackedKeys"/> (normalized with <c>:</c>),
///         the current value (if any) is treated as a file path and replaced by the file content.
///     </description></item>
///     <item><description>
///         Key normalization mirrors the default environment provider: <c>__</c> becomes <c>:</c>.
///     </description></item>
/// </list>
/// <para>
/// Precedence between plain and file-backed values is controlled by <see cref="DockerSecretsProviderOptions.PreferFileContent"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Only consider ASP.NET Core environment variables; read secrets from files under /run/secrets,
/// // keep trailing spaces but drop final newline.
/// builder.Configuration
///     .AddEnvironmentVariablesWithFiles("ASPNETCORE_", opt =>
///     {
///         opt.BasePath = "/run/secrets";
///         opt.EnforceBasePath = true;
///         opt.TrimMode = FileTrimMode.TrailingNewlinesOnly;
///         opt.PreferFileContentKeys = new HashSet&lt;string&gt;(StringComparer.OrdinalIgnoreCase)
///         {
///             "Kestrel:Certificates:Default:Password",
///             "ConnectionStrings:DbPassword"
///         };
///     })
///     .AddEnvironmentVariables("ASPNETCORE_");
/// // Env:  ASPNETCORE__KESTREL__CERTIFICATES__DEFAULT__PASSWORD_FILE=pfxpass
/// // Result: Kestrel:Certificates:Default:Password == contents of /run/secrets/pfxpass
/// </code>
/// </example>
public sealed class DockerSecretsProvider : ConfigurationProvider
{
    private readonly DockerSecretsProviderOptions _options;
    private readonly IFileSystem _fs;

    // Cache by canonical full path to avoid duplicate reads
    private readonly Dictionary<string, string> _fileCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the provider with the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Behavioral options for suffix, trimming, precedence, base path, etc.</param>
    public DockerSecretsProvider(DockerSecretsProviderOptions options, IFileSystem? fs = null)
    {
        _options = options;
        _fs = fs ?? new FileSystem();
    }

    /// <summary>
    /// Loads configuration data by reading environment variables and resolving any file-backed values.
    /// </summary>
    /// <remarks>
    /// The provider does not throw on file-read failures; it reports them via
    /// <see cref="DockerSecretsProviderOptions.OnFail"/> and skips the offending values.
    /// </remarks>
    public override void Load()
    {
        var plain = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var fileBacked = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> suppressedAllowListKeys = new(StringComparer.OrdinalIgnoreCase);

        // Keys for which we should prefer file content even when PreferFileContent == false
        var forceFileWin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var env = _options.EnvironmentTarget is EnvironmentVariableTarget target
              ? Environment.GetEnvironmentVariables(target)
              : Environment.GetEnvironmentVariables();

        // 1) Scan all environment variables once
        foreach (DictionaryEntry entry in env)
        {
            string rawKey = (string)entry.Key!;
            if (!string.IsNullOrEmpty(_options.Prefix) && !rawKey.StartsWith(_options.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawValue = (string?)entry.Value;
            var normalizedKey = Normalize(
                _options.Prefix is { Length: > 0 } && rawKey.StartsWith(_options.Prefix, StringComparison.OrdinalIgnoreCase)
                    ? rawKey.Substring(_options.Prefix.Length)
                    : rawKey);

            if (!string.IsNullOrEmpty(_options.Suffix) && rawKey.EndsWith(_options.Suffix, StringComparison.OrdinalIgnoreCase))
            {
                // clearer extraction: remove prefix (if any) and suffix explicitly before normalization
                var prefixLen = _options.Prefix?.Length ?? 0;
                var suffixLen = _options.Suffix.Length;
                var withoutPrefix = rawKey.Substring(prefixLen, rawKey.Length - prefixLen - suffixLen);
                var baseKey = Normalize(withoutPrefix);

                if (!string.IsNullOrWhiteSpace(rawValue))
                {
                    _fileCache.TryGetValue(rawValue!, out string? value);
                    if (value == null)
                    {
                        try
                        {
                            _fileCache[rawValue] = value = DockerSecretsProviderHelpers.LoadFile(
                                rawValue,
                                _fs,
                                _options.BasePath,
                                _options.EnforceBasePath,
                                _options.MaxFileBytes,
                                _options.Encoding
                            );
                        }
                        catch (Exception ex)
                        {
                            _options.OnFail?.Invoke(ex);
                        }
                    }
                    if (value != null)
                    {
                        value = DockerSecretsProviderHelpers.ApplyTrim(value, _options.TrimMode);
                        fileBacked[baseKey] = value;
                    }
                }
                continue; // never expose *_FILE
            }

            plain[normalizedKey] = rawValue;
        }
        foreach (var key in _options.AdditionalFileBackedKeys)
        {
            if (plain.TryGetValue(key, out var maybePath) && !string.IsNullOrWhiteSpace(maybePath))
            {
                _fileCache.TryGetValue(maybePath, out string? value);
                if (value == null)
                {
                    try
                    {
                        _fileCache[maybePath] = value = DockerSecretsProviderHelpers.LoadFile(
                            maybePath,
                            _fs,
                            _options.BasePath,
                            _options.EnforceBasePath,
                            _options.MaxFileBytes,
                            _options.Encoding
                        );
                    }
                    catch (Exception ex)
                    {
                        _options.OnFail?.Invoke(ex);
                    }
                }
                if (value != null)
                {
                    value = DockerSecretsProviderHelpers.ApplyTrim(value, _options.TrimMode);
                    fileBacked[key] = value;
                    forceFileWin.Add(key); // path string should be replaced by contents
                }
                else
                {
                    // File failed to load -> suppress the plain value for this allow-listed key
                    forceFileWin.Add(key);
                    suppressedAllowListKeys.Add(key);
                }
            }
        }

        // 3) Merge
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var allKeys = new HashSet<string>(plain.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(fileBacked.Keys);

        foreach (var key in allKeys)
        {
            plain.TryGetValue(key, out var plainValue);
            fileBacked.TryGetValue(key, out var fileValue);

            var preferFile =
                _options.PreferFileContent ||
                forceFileWin.Contains(key) ||
                _options.PreferFileContentKeys.Contains(key);

            if (preferFile)
            {
                if (fileValue is not null) data[key] = fileValue;
                // allow-listed path failed → skip this provider’s contribution entirely
                // (do not add data[key]; let other sources supply it if present)
                else if (suppressedAllowListKeys.Contains(key)) continue;
                else data[key] = plainValue;
            }
            else
            {
                data[key] = plainValue ?? fileValue;
            }
        }

        Data = data;
    }

    private static string Normalize(string name) => name.Replace("__", ":", StringComparison.Ordinal);
}
