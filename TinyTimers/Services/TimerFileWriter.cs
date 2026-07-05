using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TinyTimers.Services;

internal static class TimerFileWriter
{
    // OBS's Text (GDI+) source doesn't handle a UTF-8 byte-order mark well - it can render
    // as blank/garbled text instead of stripping it - so write plain UTF-8 without one.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static readonly string DefaultDirectory = Path.Combine(AppPaths.DataDirectory, "Timer Text Files");

    public static readonly string ManifestPath = Path.Combine(AppPaths.DataDirectory, "file-manifest.txt");

    private static string _outputDirectory = DefaultDirectory;

    public static string OutputDirectory => _outputDirectory;

    public static void Configure(string? customDirectory)
    {
        _outputDirectory = string.IsNullOrWhiteSpace(customDirectory) ? DefaultDirectory : customDirectory;
    }

    public static string GetFilePath(string name) => GetFilePathIn(_outputDirectory, name);

    public static string GetFilePathIn(string folder, string name) => Path.Combine(folder, $"{Slugify(name)}.txt");

    /// <summary>Single shared file that always reflects whichever regular timer is currently
    /// "active" (its linked app is in the foreground), so a streaming app can point at one
    /// fixed file instead of switching between individual timers' files.</summary>
    public static string ActiveAppFilePath => GetActiveAppFilePathIn(_outputDirectory);

    public static string GetActiveAppFilePathIn(string folder) => Path.Combine(folder, "active_app.txt");

    public static void Write(string name, string text)
    {
        try
        {
            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(GetFilePath(name), text, Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Creates active_app.txt (empty) if it doesn't already exist yet.</summary>
    public static void EnsureActiveAppFileExists()
    {
        try
        {
            Directory.CreateDirectory(_outputDirectory);
            if (!File.Exists(ActiveAppFilePath))
                File.WriteAllText(ActiveAppFilePath, "", Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void WriteActiveApp(string text)
    {
        try
        {
            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(ActiveAppFilePath, text, Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static void Delete(string name)
    {
        try
        {
            var path = GetFilePath(name);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Overwrites the manifest of exact file paths the app currently considers "in use",
    /// used by the uninstaller to clean up timer files wherever they've ended up over time.</summary>
    public static void WriteManifest(IEnumerable<string> filePaths)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllLines(ManifestPath, filePaths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Deletes any .txt file in the given folder that doesn't belong to a known timer.</summary>
    public static int RemoveOrphanedFiles(string folder, IReadOnlyCollection<string> validPaths)
    {
        if (!Directory.Exists(folder))
            return 0;

        var valid = new HashSet<string>(validPaths, StringComparer.OrdinalIgnoreCase);
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(folder, "*.txt"))
        {
            if (valid.Contains(file))
                continue;

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return removed;
    }

    /// <summary>Turns a display name into a lowercase, dash-separated, filesystem-safe slug.</summary>
    public static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c is ' ' or '-' or '_')
                sb.Append('-');
        }

        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return slug.Length == 0 ? "timer" : slug;
    }

    /// <summary>Derives a human-readable name back out of a slugged filename, e.g. "cool-timer.txt" -> "Cool Timer".</summary>
    public static string NameFromFilePath(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        var words = stem
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]);

        var name = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(name) ? stem : name;
    }
}
