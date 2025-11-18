using System.Text;

namespace Vilip.Utilities.Providers.DockerSecrets;


/// <summary>
/// Controls whitespace trimming applied to file contents.
/// </summary>
/// <remarks>
/// Useful when reading Docker/Kubernetes secrets which often end with a newline.
/// </remarks>
/// <list type="bullet">
///   <item><description><see cref="None"/>: keep content as-is.</description></item>
///   <item><description><see cref="TrailingWhitespace"/>: trim all trailing whitespace (default).</description></item>
///   <item><description><see cref="TrailingNewlinesOnly"/>: remove only final <c>\r</c>/<c>\n</c> characters, preserving spaces/tabs.</description></item>
/// </list>
public enum FileTrimMode
{
    None,
    TrailingWhitespace,
    TrailingNewlinesOnly
}


/// <summary>
/// Options controlling how <see cref="DockerSecretsProvider"/> interprets
/// environment variables as file-backed values.
/// </summary>
/// <remarks>
/// <para>
/// This provider lets you supply configuration values from files referenced by environment variables.
/// Two mechanisms are supported:
/// </para>
/// <list type="number">
///   <item>
///     <description>
///         A suffix (default: <c>_FILE</c>) appended to an environment variable name to indicate the env
///         value is a file path (e.g. <c>MY_KEY_FILE=/run/secrets/my_key</c>). The provider strips the
///         suffix and stores the <em>file contents</em> under the base key (<c>MY_KEY</c>).
///     </description>
///   </item>
///   <item>
///     <description>
///         An allow-list of normalized keys (<see cref="AdditionalFileBackedKeys"/>) that are always
///         treated as file paths even without the suffix.
///     </description>
///   </item>
/// </list>
/// <para>
/// Keys are normalized like the default environment provider: double-underscore <c>__</c> becomes
/// section separator <c>:</c> (e.g., <c>A__B__C</c> → <c>A:B:C</c>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// builder.Configuration.AddEnvironmentVariablesWithFiles(opt =>
/// {
///     opt.Suffix = "_FILE";
///     opt.PreferFileContent = true;
///     opt.TrimMode = FileTrimMode.TrailingWhitespace;
///     opt.BasePath = "/run/secrets";
///     opt.AdditionalFileBackedKeys = new HashSet&lt;string&gt;(StringComparer.OrdinalIgnoreCase)
///     {
///         "Kestrel:Certificates:Default:Password",
///         "ConnectionStrings:DbPassword"
///     };
///     opt.OnFail = ex => Console.Error.WriteLine($"[config warning] {ex.Message}");
/// });
/// // Env:  KESTREL__CERTIFICATES__DEFAULT__PASSWORD_FILE=/run/secrets/pfxpass
/// // Result key: Kestrel:Certificates:Default:Password == file contents
/// </code>
/// </example>
public sealed class DockerSecretsProviderOptions
{
    /// <summary>
    /// Suffix that indicates a file-backed environment variable (e.g., <c>MY_KEY_FILE</c>).
    /// Set to empty (<c>""</c>) to disable suffix handling entirely.
    /// </summary>
    public string Suffix { get; set; } = "_FILE";

    /// <summary>
    /// Optional prefix filter applied to environment variable names before processing (e.g., <c>"ASPNETCORE_"</c>).
    /// Only variables starting with this prefix are considered by the provider.
    /// </summary>
    /// <remarks>
    /// The prefix, when set, is stripped from keys before normalization
    /// (so <c>ASPNETCORE__Kestrel__Endpoints</c> becomes <c>Kestrel:Endpoints</c>).
    /// </remarks>
    public string? Prefix { get; set; }

    /// <summary>
    /// Normalized keys (use <c>:</c> separators) that should be treated as file-backed even without the suffix.
    /// For example: <c>"Kestrel:Certificates:Default:Password"</c>.
    /// </summary>
    public ISet<string> AdditionalFileBackedKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Keys (normalized with <c>:</c>) for which file content should win even if
    /// <see cref="PreferFileContent"/> is <see langword="false"/>.
    /// </summary>
    public ISet<string> PreferFileContentKeys { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// If <see langword="true"/>, the content read from a file overrides a plain environment variable value for the same key.
    /// If <see langword="false"/>, a plain environment variable value takes precedence over file content.
    /// </summary>
    public bool PreferFileContent { get; set; } = true;

    /// <summary>
    /// Trimming policy applied to content read from files. Default is <see cref="FileTrimMode.TrailingWhitespace"/>.
    /// </summary>
    public FileTrimMode TrimMode { get; set; } = FileTrimMode.TrailingWhitespace;

    /// <summary>
    /// The text encoding used when reading files. Defaults to UTF-8 without BOM.
    /// </summary>
    public Encoding? Encoding { get; set; } = Utf8NoBom;

    /// <summary>
    /// Optional base path used to resolve relative file paths. Defaults to the current working directory
    /// at the moment the options object is created.
    /// </summary>
    public string? BasePath { get; set; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// If <see langword="true"/>, ensures that any resolved file path lies within <see cref="BasePath"/>.
    /// </summary>
    /// <remarks>
    /// Guards against accidental directory traversal when paths are relative or include environment expansions.
    /// When enabled, a path that escapes <see cref="BasePath"/> is reported via <see cref="OnFail"/> and skipped.
    /// </remarks>
    public bool EnforceBasePath { get; set; } = false;

    /// <summary>
    /// Maximum allowed size (in bytes) for a file-backed value.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>128&nbsp;KiB</c>. Set to <see langword="null"/> to disable the size check.
    /// Files exceeding the limit are reported via <see cref="OnFail"/> and skipped.
    /// </remarks>
    public long? MaxFileBytes { get; set; } = 128 * 1024; // 128 KiB sane default

    /// <summary>
    /// Invoked when a file-read issue occurs (missing file, access denied, I/O error, base-path violation, size limit).
    /// The provider does not throw; it reports the failure via this callback and skips the offending value.
    /// </summary>
    public Action<Exception>? OnFail { get; set; }

    /// <summary>
    /// Optional scope for reading environment variables (Process/User/Machine). When unset, the runtime default is used.
    /// </summary>
    public EnvironmentVariableTarget? EnvironmentTarget { get; set; } // null => default

    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
}
