using System.IO;

namespace TinyTimers.Services;

/// <summary>Tracks every folder the user has ever pointed timer files at, so "Clear old timers"
/// and uninstall cleanup can find orphaned files left behind in folders no longer in active use.</summary>
internal static class KnownFoldersStore
{
    private static readonly string FilePath = Path.Combine(AppPaths.DataDirectory, "known-folders.txt");

    public static void Track(string folder)
    {
        var folders = Load();
        if (folders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            return;

        folders.Add(folder);
        Save(folders);
    }

    public static List<string> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? File.ReadAllLines(FilePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void Save(List<string> folders)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllLines(FilePath, folders);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
