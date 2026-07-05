using System.IO;
using System.Text.Json;
using TinyTimers.Models;

namespace TinyTimers.Services;

internal sealed class TimerRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public TimerKind Kind { get; set; }
    public TimeSpan Elapsed { get; set; }
    public TimeSpan CountdownDuration { get; set; }
    public string? LinkedAppPath { get; set; }
    public string? SoundFilePath { get; set; }
}

/// <summary>A timer-files folder's own independent set of timers - their names, elapsed
/// times, linked apps, etc. Switching the timer-files folder switches which profile is
/// active, without losing the other profiles' data.</summary>
internal sealed class TimerFolderProfile
{
    public string FolderPath { get; set; } = "";
    public List<TimerRecord> Timers { get; set; } = [];
}

/// <summary>Persists timer state (elapsed times, linked apps, etc.) keyed by the timer-files
/// folder they belong to, so switching folders and switching back restores exactly what was
/// there before instead of losing it.</summary>
internal static class TimerStore
{
    private static readonly string DataFilePath = Path.Combine(AppPaths.DataDirectory, "timers.json");

    private sealed class StoreFile
    {
        public List<TimerFolderProfile> Profiles { get; set; } = [];
    }

    public static List<TimerRecord> LoadRecordsForFolder(string folderPath)
    {
        var profile = LoadAllProfiles().FirstOrDefault(p => IsSameFolder(p.FolderPath, folderPath));
        return profile?.Timers ?? [];
    }

    public static void SaveRecordsForFolder(string folderPath, IEnumerable<TimerItem> timers)
    {
        var profiles = LoadAllProfiles();
        var profile = profiles.FirstOrDefault(p => IsSameFolder(p.FolderPath, folderPath));
        if (profile is null)
        {
            profile = new TimerFolderProfile { FolderPath = folderPath };
            profiles.Add(profile);
        }

        profile.Timers = timers.Select(ToRecord).ToList();
        SaveAllProfiles(profiles);
    }

    public static List<TimerFolderProfile> LoadAllProfiles()
    {
        if (!File.Exists(DataFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(DataFilePath);
            using var doc = JsonDocument.Parse(json);

            // Pre-migration data is a flat JSON array of records for whatever single folder
            // was active at the time (the app only ever supported one folder's worth of state
            // back then). Attribute it to the currently configured folder and migrate forward.
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyRecords = JsonSerializer.Deserialize<List<TimerRecord>>(json) ?? [];
                return legacyRecords.Count == 0
                    ? []
                    : [new TimerFolderProfile { FolderPath = TimerFileWriter.OutputDirectory, Timers = legacyRecords }];
            }

            var file = JsonSerializer.Deserialize<StoreFile>(json);
            return file?.Profiles ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static void SaveAllProfiles(List<TimerFolderProfile> profiles)
    {
        var json = JsonSerializer.Serialize(new StoreFile { Profiles = profiles }, new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(DataFilePath, json);
    }

    private static TimerRecord ToRecord(TimerItem t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Kind = t.Kind,
        Elapsed = t.CurrentElapsed,
        CountdownDuration = t.CountdownDuration,
        LinkedAppPath = t.LinkedAppPath,
        SoundFilePath = t.SoundFilePath
    };

    private static bool IsSameFolder(string a, string b) =>
        string.Equals(NormalizeFolder(a), NormalizeFolder(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeFolder(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
