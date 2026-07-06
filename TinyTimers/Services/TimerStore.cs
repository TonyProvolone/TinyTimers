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

/// <summary>Superseded storage shape kept only to migrate old data forward: it scoped a
/// separate set of timers to each folder the user had ever pointed timer files at, so
/// switching folders swapped in a different roster instead of carrying the current one along.</summary>
internal sealed class TimerFolderProfile
{
    public string FolderPath { get; set; } = "";
    public List<TimerRecord> Timers { get; set; } = [];
}

/// <summary>Persists the single roster of timers (names, elapsed times, linked apps, etc.) that
/// travels with the user regardless of which folder their live text files are currently written
/// to - the timer-files folder is just an output location, not a separate set of timers.</summary>
internal static class TimerStore
{
    private static readonly string DataFilePath = Path.Combine(AppPaths.DataDirectory, "timers.json");

    private sealed class StoreFile
    {
        public List<TimerRecord> Timers { get; set; } = [];
    }

    public static List<TimerRecord> LoadRecords()
    {
        if (!File.Exists(DataFilePath))
            return [];

        try
        {
            var json = File.ReadAllText(DataFilePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Oldest format: a flat array of records for whatever single folder was active at
            // the time (the app only ever supported one folder's worth of state back then).
            if (root.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<List<TimerRecord>>(json) ?? [];

            // Next format: one independent profile per folder ever used. Timers now travel with
            // the user across folder switches instead of being scoped to whichever folder was
            // active when they were saved, so merge every profile's timers into one flat roster
            // (keeping each Id only once, in case the same timer was ever duplicated across profiles).
            if (root.TryGetProperty("Profiles", out var profilesProp))
            {
                var profiles = JsonSerializer.Deserialize<List<TimerFolderProfile>>(profilesProp.GetRawText()) ?? [];
                var merged = profiles
                    .SelectMany(p => p.Timers)
                    .GroupBy(r => r.Id)
                    .Select(g => g.First())
                    .ToList();

                DeduplicateNames(merged);
                return merged;
            }

            var file = JsonSerializer.Deserialize<StoreFile>(json);
            return file?.Timers ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static void SaveRecords(IEnumerable<TimerItem> timers)
    {
        var storeFile = new StoreFile { Timers = timers.Select(ToRecord).ToList() };

        try
        {
            var json = JsonSerializer.Serialize(storeFile, new JsonSerializerOptions { WriteIndented = true });

            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(DataFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Two different folders could each have had their own independently-named timer
    /// that happened to share a name - allowed under the old per-folder model, since uniqueness
    /// was only enforced within one folder's own roster - but not once everything merges into a
    /// single roster, since the file a timer writes to is derived purely from its name. Renames
    /// every name after its first occurrence so nothing silently collides post-migration.</summary>
    private static void DeduplicateNames(List<TimerRecord> records)
    {
        var seenSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var slug = TimerFileWriter.Slugify(record.Name);
            var suffix = 2;
            while (!seenSlugs.Add(slug))
            {
                record.Name = $"{record.Name} ({suffix})";
                slug = TimerFileWriter.Slugify(record.Name);
                suffix++;
            }
        }
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
}
