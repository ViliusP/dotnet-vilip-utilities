using System.IO.Abstractions;
using System.Text;

namespace Vilip.Utilities.Providers.DockerSecrets.Helpers;

/// <summary>
/// Common helpers used by provider.
/// </summary>
internal static class DockerSecretsProviderHelpers
{
    internal static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    internal static string ApplyTrim(string s, FileTrimMode mode) => mode switch
    {
        FileTrimMode.None => s,
        FileTrimMode.TrailingWhitespace => s.Trim(),
        FileTrimMode.TrailingNewlinesOnly => TrimTrailingNewlines(s),
        _ => s
    };

    internal static string LoadFile(
        string pathOrTemplate,
        IFileSystem fileSystem,
        string? basePath,
        bool enforceBasePath,
        long? sizeLimit,
        Encoding? encoding
    )
    {
        var expanded = Environment.ExpandEnvironmentVariables(ExpandTilde(pathOrTemplate));
        var fullPath = ResolvePath(fileSystem, expanded, basePath);

        if (enforceBasePath && !IsUnderBasePath(fileSystem, fullPath, NormalizeBasePath(fileSystem, basePath)))
        {
            throw new IOException($"File '{fullPath}' escapes BasePath.");
        }

        if (!fileSystem.File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File '{fullPath}' does not exist.");
        }

        if (sizeLimit is long)
        {
            var info = new FileInfo(fullPath);
            if (info.Length > sizeLimit)
            {
                throw new IOException($"File '{fullPath}' exceeds size limit ({info.Length} > {sizeLimit} bytes).");
            }
        }

        var enc = encoding ?? Utf8NoBom;
        return fileSystem.File.ReadAllText(fullPath, enc);
    }

    internal static string TrimTrailingNewlines(string s)
    {
        int end = s.Length;
        while (end > 0)
        {
            char c = s[end - 1];
            if (c == '\n' || c == '\r') end--;
            else break;
        }
        return end == s.Length ? s : s[..end];
    }

    private static string ExpandTilde(string path)
    {
        return (path.Length > 0 && path[0] == '~')
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path.AsSpan(1).ToString()
            : path;
    }

    private static string ResolvePath(IFileSystem fs, string path, string? basePath)
    {
        if (fs.Path.IsPathRooted(path)) return fs.Path.GetFullPath(path);
        var root = basePath ?? fs.Directory.GetCurrentDirectory();
        return fs.Path.GetFullPath(fs.Path.Combine(root, path));
    }

    private static string NormalizeBasePath(IFileSystem fs, string? basePath)
    {
        var root = basePath ?? fs.Directory.GetCurrentDirectory();
        return fs.Path.GetFullPath(root)
                 .TrimEnd(fs.Path.DirectorySeparatorChar, fs.Path.AltDirectorySeparatorChar)
             + fs.Path.DirectorySeparatorChar;
    }

    private static bool IsUnderBasePath(IFileSystem fs, string fullPath, string normalizedBasePath)
    {
        var normFull = fs.Path.GetFullPath(fullPath);
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return normFull.StartsWith(normalizedBasePath, cmp);
    }

}
