using System.IO;
using System.Text.Json;
using TinyTimers.Models;

namespace TinyTimers.Services;

internal static class SettingsStore
{
    private static readonly string SettingsFilePath = Path.Combine(AppPaths.DataDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsFilePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(AppPaths.DataDirectory);
        File.WriteAllText(SettingsFilePath, json);
    }
}
