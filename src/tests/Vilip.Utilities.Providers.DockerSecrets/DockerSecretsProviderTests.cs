
// Serialize tests that mutate process environment variables
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Vilip.Utilities.Providers.DockerSecrets.Tests;

[CollectionDefinition(nameof(EnvIsolation), DisableParallelization = true)]
public class EnvIsolation { }

[Collection(nameof(EnvIsolation))]
public sealed class DockerSecretsProviderTest : IDisposable
{
    private readonly string _tempDir;

    public DockerSecretsProviderTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fb-env-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originals = new(StringComparer.OrdinalIgnoreCase);

        public EnvScope Set(string key, string? value)
        {
            if (!_originals.ContainsKey(key)) _originals[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
            return this;
        }

        public void Dispose()
        {
            foreach (var kvp in _originals) Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    private string WriteFile(string name, string content, Encoding? enc = null)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content, enc ?? new UTF8Encoding(false));
        return path;
    }

    private static IConfiguration BuildConfig(Action<DockerSecretsProviderOptions>? configure = null)
    {
        var options = new DockerSecretsProviderOptions();
        configure?.Invoke(options);

        var cfg = new ConfigurationBuilder()
            .Add(new DockerSecretsSource(options))
            .Build();

        return cfg;
    }

    // Small helper to capture failures
    private sealed class FailSink
    {
        public readonly List<Exception> Errors = new();
        public Action<Exception> Hook => ex => Errors.Add(ex);
    }

    [Fact]
    public void Suffix_Loads_File_Content_And_Stores_Under_Base_Key()
    {
        using var env = new EnvScope();
        var secretPath = WriteFile("s.txt", "from-file\n"); // newline to test trim
        env.Set("MYAPP__SECRET_FILE", secretPath);

        var cfg = BuildConfig(opt =>
        {
            opt.Suffix = "_FILE";
            opt.TrimMode = FileTrimMode.TrailingWhitespace;
        });

        Assert.Equal("from-file", cfg["MyApp:Secret"]);
        Assert.Null(cfg["MyApp:Secret_FILE"]); // *_FILE key is not exposed
    }

    [Fact]
    public void PreferFileContent_True_File_Wins_Over_Plain()
    {
        using var env = new EnvScope();
        var p = WriteFile("p.txt", "file-value");
        env.Set("APP__KEY", "literal");
        env.Set("APP__KEY_FILE", p);

        var cfg = BuildConfig(opt => opt.PreferFileContent = true);

        Assert.Equal("file-value", cfg["App:Key"]);
    }

    [Fact]
    public void PreferFileContent_False_Plain_Wins_Over_File()
    {
        using var env = new EnvScope();
        var p = WriteFile("p.txt", "file-value");
        env.Set("APP__KEY", "literal");
        env.Set("APP__KEY_FILE", p);

        var cfg = BuildConfig(opt => opt.PreferFileContent = false);

        Assert.Equal("literal", cfg["App:Key"]);
    }

    [Fact]
    public void AdditionalFileBackedKeys_Treats_Value_As_Path_Without_Suffix()
    {
        using var env = new EnvScope();
        var p = WriteFile("dbpass.txt", "db-pass-123\n");
        env.Set("CONNECTIONSTRINGS__DBPASSWORD", Path.GetFileName(p)); // relative path

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.TrimMode = FileTrimMode.TrailingWhitespace;
            opt.AdditionalFileBackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            "ConnectionStrings:DbPassword"
            };
        });

        Assert.Equal("db-pass-123", cfg["ConnectionStrings:DbPassword"]);
    }

    [Fact]
    public void Relative_Path_Resolves_Against_BasePath()
    {
        using var env = new EnvScope();
        var p = WriteFile("rel.txt", "rel-content");
        env.Set("MY__VAL_FILE", "rel.txt");

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
        });

        Assert.Equal("rel-content", cfg["My:Val"]);
    }

    [Fact]
    public void Missing_File_Is_Reported_And_Skipped()
    {
        using var env = new EnvScope();
        var sink = new FailSink();
        env.Set("MISSING__SECRET_FILE", "nope.txt");

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["Missing:Secret"]);
        Assert.Single(sink.Errors);
        Assert.Contains("does not exist", sink.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_File_Yields_Empty_String_After_TrailingWhitespace_Trim()
    {
        using var env = new EnvScope();
        var p = WriteFile("empty.txt", "\n"); // newline only
        env.Set("X__Y_FILE", p);

        var cfg = BuildConfig(opt => opt.TrimMode = FileTrimMode.TrailingWhitespace);

        Assert.Equal(string.Empty, cfg["X:Y"]);
    }

    [Theory]
    [InlineData(FileTrimMode.None, "abc \n\t", "abc \n\t")]
    [InlineData(FileTrimMode.TrailingWhitespace, "abc  \n\t", "abc")]
    [InlineData(FileTrimMode.TrailingNewlinesOnly, "abc \n\r", "abc ")]
    public void TrimMode_Variants_Apply(FileTrimMode mode, string content, string expected)
    {
        using var env = new EnvScope();
        var p = WriteFile("t.txt", content);
        env.Set("T__K_FILE", p);

        var cfg = BuildConfig(opt => opt.TrimMode = mode);

        Assert.Equal(expected, cfg["T:K"]);
    }

    [Fact]
    public void Encoding_Can_Be_Customized()
    {
        using var env = new EnvScope();
        var content = "ユニコード-ελληνικά";
        var p = WriteFile("u.txt", content, Encoding.Unicode); // UTF-16LE
        env.Set("ENC__VAL_FILE", p);

        var cfg = BuildConfig(opt =>
        {
            opt.Encoding = Encoding.Unicode; // read using UTF-16
        });

        Assert.Equal(content, cfg["Enc:Val"]);
    }

    [Fact]
    public void Suffix_Disabled_Treats_File_Var_As_Normal_Value()
    {
        using var env = new EnvScope();
        env.Set("NOFILE__SECRET_FILE", "should-not-be-read-as-path");

        var cfg = BuildConfig(opt => opt.Suffix = "");

        // With suffix disabled, the key is normalized but not stripped:
        // "NOFILE__SECRET_FILE" -> "NOFILE:SECRET_FILE"
        Assert.Equal("should-not-be-read-as-path", cfg["NOFILE:SECRET_FILE"]);
        Assert.Null(cfg["NOFILE:SECRET"]); // nothing applied to base key
    }

    [Fact]
    public void AllowList_Replaces_Path_String_Even_When_PreferFileContent_False()
    {
        using var env = new EnvScope();
        var p = WriteFile("allow.txt", "ALLOW");
        env.Set("MY__ALLOW", p); // env value equals the path (common pattern)

        var cfg = BuildConfig(opt =>
        {
            opt.PreferFileContent = false; // normally env would win
            opt.AdditionalFileBackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            "My:Allow"
            };
        });

        Assert.Equal("ALLOW", cfg["My:Allow"]);
    }

    [Fact]
    public void PreferFileContentKeys_Overrides_Global_Preference()
    {
        using var env = new EnvScope();
        var p = WriteFile("tok.txt", "from-file");
        env.Set("API__TOKEN", "from-env");
        env.Set("API__TOKEN_FILE", p);

        var cfg = BuildConfig(opt =>
        {
            opt.PreferFileContent = false; // env would normally win
            opt.PreferFileContentKeys.Add("Api:Token");
        });

        Assert.Equal("from-file", cfg["Api:Token"]);
    }

    [Fact]
    public void Normalization_Mirrors_DefaultEnvProvider()
    {
        using var env = new EnvScope();
        env.Set("A__B__C", "val");
        var cfg = BuildConfig();

        Assert.Equal("val", cfg["A:B:C"]);
    }

    [Fact]
    public void Prefix_Filters_And_Is_Stripped()
    {
        using var env = new EnvScope();
        var p = WriteFile("pfx.txt", "pv");
        env.Set("ASPNETCORE_Kestrel__Certificates__Default__Password_FILE", p);
        env.Set("OTHER__IGNORED_FILE", p); // should be ignored by prefix

        var cfg = BuildConfig(opt =>
        {
            opt.Prefix = "ASPNETCORE_";
            opt.Suffix = "_FILE";
        });

        Assert.Equal("pv", cfg["Kestrel:Certificates:Default:Password"]);
        Assert.Null(cfg["Other:Ignored"]);
    }

    [Fact]
    public void EnforceBasePath_Reports_And_Skips_Path_Escape()
    {
        using var env = new EnvScope();
        var sink = new FailSink();

        // Create a file outside of _tempDir to ensure the path resolves
        var outsideDir = Path.Combine(_tempDir, "..");
        var outsidePath = Path.GetFullPath(Path.Combine(outsideDir, "outside.txt"));
        File.WriteAllText(outsidePath, "nope");

        env.Set("APP__K_FILE", "../outside.txt"); // try to escape

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.EnforceBasePath = true;
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["App:K"]);
        Assert.Single(sink.Errors);
        Assert.Contains("escapes BasePath", sink.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MaxFileBytes_Rejects_Large_Files()
    {
        using var env = new EnvScope();
        var big = WriteFile("big.bin", new string('x', 10_000));
        env.Set("APP__L_FILE", big);

        var sink = new FailSink();
        var cfg = BuildConfig(opt =>
        {
            opt.MaxFileBytes = 1024; // 1 KiB
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["App:L"]);
        Assert.Single(sink.Errors);
        Assert.Contains("exceeds size limit", sink.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prefix_And_Suffix_Combine_Correctly()
    {
        using var env = new EnvScope();
        var p = WriteFile("pfx.txt", "pv");
        env.Set("ASPNETCORE_My__Key_FILE", p);

        var cfg = BuildConfig(opt => { opt.Prefix = "ASPNETCORE_"; opt.Suffix = "_FILE"; });

        Assert.Equal("pv", cfg["My:Key"]);
        Assert.Null(cfg["ASPNETCORE_My:Key"]); // prefix must be stripped
    }

    [Fact]
    public void Empty_File_Path_Is_Ignored()
    {
        using var env = new EnvScope();
        env.Set("APP__K", "plain");
        env.Set("APP__K_FILE", ""); // empty

        var cfg = BuildConfig();

        Assert.Equal("plain", cfg["App:K"]);
    }

    [Fact]
    public void AllowList_Overwrites_Suffix_For_Same_Key()
    {
        using var env = new EnvScope();
        var f1 = WriteFile("f1.txt", "from-suffix");
        var f2 = WriteFile("f2.txt", "from-allow");
        env.Set("APP__K_FILE", f1);
        env.Set("APP__K", f2); // plain value used as allow-list path

        var cfg = BuildConfig(opt =>
        {
            opt.AdditionalFileBackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "App:K" };
        });

        Assert.Equal("from-allow", cfg["App:K"]); // allow-list wins (processed after suffix)
    }

    [Fact]
    public void Expands_Environment_Variables_And_Tilde()
    {
        using var env = new EnvScope();

        // ---- %VAR% expansion (works on Windows & Unix in .NET) ----
        var secret = WriteFile("s.txt", "ok");
        env.Set("FBP_BASE", _tempDir);

        var expandedRef = OperatingSystem.IsWindows()
            ? @"%FBP_BASE%\s.txt"
            : "%FBP_BASE%/s.txt";

        env.Set("APP__S_FILE", expandedRef);

        var cfg1 = BuildConfig();
        Assert.Equal("ok", cfg1["App:S"]);

        // ---- ~ expansion (provider uses SpecialFolder.UserProfile) ----
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
        {
            var fileName = "fb-tilde-" + Guid.NewGuid().ToString("N") + ".txt";
            var homeFile = Path.Combine(home, fileName);
            File.WriteAllText(homeFile, "ok-tilde");
            try
            {
                env.Set("APP__T_FILE", "~/" + fileName);

                var cfg2 = BuildConfig();
                Assert.Equal("ok-tilde", cfg2["App:T"]);
            }
            finally
            {
                try { File.Delete(homeFile); } catch { /* ignore */ }
            }
        }
    }

    [Fact]
    public void BasePath_Defaults_To_CurrentDirectory()
    {
        using var env = new EnvScope();
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            var p = WriteFile("cd.txt", "cdv");
            env.Set("APP__CD_FILE", "cd.txt"); // relative to CWD

            var cfg = BuildConfig(opt => opt.BasePath = null); // rely on default
            Assert.Equal("cdv", cfg["App:Cd"]);
        }
        finally { Directory.SetCurrentDirectory(cwd); }
    }

    [Fact]
    public void Key_Sets_Are_Case_Insensitive()
    {
        using var env = new EnvScope();
        var p = WriteFile("v.txt", "FILE");
        env.Set("app__PaSsWoRd", "from-env");
        env.Set("app__password_FILE", p);

        var cfg = BuildConfig(opt =>
        {
            opt.PreferFileContent = false;
            opt.PreferFileContentKeys.Add("APP:password"); // different case
        });

        Assert.Equal("FILE", cfg["App:Password"]);
    }

    [Fact]
    public void Directory_Path_Is_Reported_And_Skipped()
    {
        using var env = new EnvScope();
        var sink = new FailSink();
        var dir = Path.Combine(_tempDir, "adir");
        Directory.CreateDirectory(dir);
        env.Set("APP__DIR_FILE", dir);

        var cfg = BuildConfig(opt =>
        {
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["App:Dir"]);
        Assert.Single(sink.Errors);
        // Could be "Could not find a part of the path" or similar depending on platform
    }

    [Fact]
    public void Missing_File_Does_Not_Wipe_Plain_Value()
    {
        using var env = new EnvScope();
        var sink = new FailSink();
        env.Set("APP__K", "plain");
        env.Set("APP__K_FILE", "missing.txt");

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.OnFail = sink.Hook;
        });

        Assert.Equal("plain", cfg["App:K"]);
        Assert.Single(sink.Errors);
    }

    [Fact]
    public void AllowList_Works_When_Suffix_Disabled()
    {
        using var env = new EnvScope();
        var p = WriteFile("a.txt", "AL");
        env.Set("M__K", Path.GetFileName(p));

        var cfg = BuildConfig(opt =>
        {
            opt.Suffix = "";           // disable suffix
            opt.BasePath = _tempDir;
            opt.AdditionalFileBackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "M:K" };
        });

        Assert.Equal("AL", cfg["M:K"]);
    }

    [Fact]
    public void PreferFileContentKeys_Works_Without_Suffix_When_AllowListed()
    {
        using var env = new EnvScope();
        var p = WriteFile("pw.txt", "F");
        env.Set("Kestrel__Certificates__Default__Password", Path.GetFileName(p));

        var cfg = BuildConfig(opt =>
        {
            opt.PreferFileContent = false;
            opt.PreferFileContentKeys.Add("kestrel:certificates:default:password");
            opt.AdditionalFileBackedKeys.Add("Kestrel:Certificates:Default:Password");
            opt.BasePath = _tempDir;
        });

        Assert.Equal("F", cfg["Kestrel:Certificates:Default:Password"]);
    }

    [Theory]
    [InlineData("one\r\ntwo\r\n", "one\r\ntwo")]
    [InlineData("one\ntwo\n\n", "one\ntwo")]
    [InlineData("end\r", "end")]
    public void TrailingNewlinesOnly_Removes_Only_LineTerminators(string content, string expected)
    {
        using var env = new EnvScope();
        var p = WriteFile("nl.txt", content);
        env.Set("N__L_FILE", p);

        var cfg = BuildConfig(opt => opt.TrimMode = FileTrimMode.TrailingNewlinesOnly);

        Assert.Equal(expected, cfg["N:L"]);
    }

    [Fact]
    public void FileKey_Is_Not_Emitted_With_Prefix()
    {
        using var env = new EnvScope();
        var p = WriteFile("x.txt", "v");
        env.Set("ASPNETCORE_App__X_FILE", p);

        var cfg = BuildConfig(opt => { opt.Prefix = "ASPNETCORE_"; opt.Suffix = "_FILE"; });

        Assert.Null(cfg["ASPNETCORE_App:X"]);
        Assert.Equal("v", cfg["App:X"]);
    }

    [Fact]
    public void Default_Encoding_Is_Utf8_Without_Bom_When_Null()
    {
        using var env = new EnvScope();
        var s = "žąčęį Š";
        var p = WriteFile("utf8.txt", s, new UTF8Encoding(false));
        env.Set("E__K_FILE", p);

        var cfg = BuildConfig(opt => opt.Encoding = null);

        Assert.Equal(s, cfg["E:K"]);
    }

    [Fact]
    public void AllowList_Missing_Path_Is_Reported_And_Skipped()
    {
        using var env = new EnvScope();
        var sink = new FailSink();
        env.Set("SERVICE__APIKEY", "missing.txt"); // not present

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.AdditionalFileBackedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
            "Service:ApiKey"
            };
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["Service:ApiKey"]);                   // allow-listed missing -> skipped
        Assert.Single(sink.Errors);
        Assert.IsType<FileNotFoundException>(sink.Errors[0]);
    }

    [Fact]
    public void AllowList_Failure_Does_Not_Wipe_Suffix_Value()
    {
        using var env = new EnvScope();
        var sink = new FailSink();

        // Suffix (_FILE) points to a valid file
        var f1 = WriteFile("ok.txt", "from-suffix");

        // Allow-listed key uses the plain value as a path, but that path is missing
        env.Set("APP__K_FILE", f1);            // valid file-backed value
        env.Set("APP__K", "missing.txt");      // allow-list path will fail to load

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.AdditionalFileBackedKeys.Add("App:K"); // treat APP__K as a path
            opt.PreferFileContent = true;
            opt.OnFail = sink.Hook;
        });

        // EXPECTATION:
        // - The valid suffix value must remain.
        // - The allow-list failure must be reported via OnFail (exact type/msg may vary).
        Assert.Equal("from-suffix", cfg["App:K"]);
        Assert.Single(sink.Errors);
        Assert.Contains("does not exist", sink.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AllowList_Success_Overrides_Suffix_When_Both_Present()
    {
        using var env = new EnvScope();

        var f1 = WriteFile("suffix.txt", "from-suffix");
        var f2 = WriteFile("allow.txt", "from-allow");

        env.Set("APP__K_FILE", f1); // suffix value present
        env.Set("APP__K", f2);      // allow-list will load and should override

        var cfg = BuildConfig(opt =>
        {
            opt.AdditionalFileBackedKeys.Add("App:K");
        });

        // EXPECTATION: allow-list wins over suffix when it loads successfully
        Assert.Equal("from-allow", cfg["App:K"]);
    }

    [Fact]
    public void AllowList_And_Suffix_Both_Fail_Results_In_No_Value()
    {
        using var env = new EnvScope();
        var sink = new FailSink();

        env.Set("APP__K_FILE", Path.Combine(_tempDir, "nope1.txt")); // missing
        env.Set("APP__K", Path.Combine(_tempDir, "nope2.txt"));      // missing

        var cfg = BuildConfig(opt =>
        {
            opt.BasePath = _tempDir;
            opt.AdditionalFileBackedKeys.Add("App:K");
            opt.OnFail = sink.Hook;
        });

        Assert.Null(cfg["App:K"]);
        Assert.True(sink.Errors.Count >= 1); // at least one failure reported
    }

}
