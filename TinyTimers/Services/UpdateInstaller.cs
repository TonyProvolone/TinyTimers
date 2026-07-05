using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace TinyTimers.Services;

/// <summary>Downloads a release installer in the background and defers actually running it until
/// the app is next restarted, so an in-progress session is never interrupted. The installer
/// (Installer/TinyTimers.iss) only ever overwrites the app's own exe - it never touches the
/// %LocalAppData%\TinyTimers data directory where timers and settings live - so applying a
/// downloaded update can't wipe out the user's existing data.</summary>
internal static class UpdateInstaller
{
    private static readonly string UpdatesDirectory = Path.Combine(AppPaths.DataDirectory, "Updates");
    private static readonly string PendingMarkerPath = Path.Combine(AppPaths.DataDirectory, "pending-update.json");

    private sealed record PendingUpdate(string Version, string InstallerPath);

    /// <summary>Downloads the installer for the given release and records it as pending. Safe to call
    /// repeatedly - re-downloading the same or an older version than what's already pending is skipped.</summary>
    public static async Task<bool> DownloadAsync(UpdateChecker.UpdateInfo info, CancellationToken ct = default)
    {
        if (info.InstallerDownloadUrl is null)
            return false;

        if (TryGetPendingInstaller(out _, out var pendingVersionText)
            && Version.TryParse(pendingVersionText, out var pendingVersion)
            && pendingVersion >= info.Version)
        {
            return false;
        }

        Directory.CreateDirectory(UpdatesDirectory);
        ClearDownloadedInstallers();

        var fileName = Path.GetFileName(new Uri(info.InstallerDownloadUrl).LocalPath);
        var destPath = Path.Combine(UpdatesDirectory, fileName);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TinyTimers-UpdateChecker");

        var tempPath = destPath + ".download";
        await using (var fileStream = File.Create(tempPath))
        await using (var httpStream = await http.GetStreamAsync(info.InstallerDownloadUrl, ct).ConfigureAwait(false))
        {
            await httpStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }
        File.Move(tempPath, destPath, overwrite: true);

        var marker = new PendingUpdate(info.Version.ToString(), destPath);
        File.WriteAllText(PendingMarkerPath, JsonSerializer.Serialize(marker));
        return true;
    }

    public static bool TryGetPendingInstaller(out string installerPath, out string version)
    {
        installerPath = string.Empty;
        version = string.Empty;

        if (!File.Exists(PendingMarkerPath))
            return false;

        try
        {
            var marker = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(PendingMarkerPath));
            if (marker is null || !File.Exists(marker.InstallerPath))
            {
                ClearPending();
                return false;
            }

            installerPath = marker.InstallerPath;
            version = marker.Version;
            return true;
        }
        catch (JsonException)
        {
            ClearPending();
            return false;
        }
    }

    public static void ClearPending()
    {
        try { if (File.Exists(PendingMarkerPath)) File.Delete(PendingMarkerPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        ClearDownloadedInstallers();
    }

    private static void ClearDownloadedInstallers()
    {
        if (!Directory.Exists(UpdatesDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(UpdatesDirectory))
        {
            try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Runs the downloaded installer unattended and exits this process immediately so the
    /// installer can overwrite the running exe. The installer relaunches the app itself once done
    /// (see the [Run] section in TinyTimers.iss).</summary>
    public static void LaunchInstallerAndExit(string installerPath)
    {
        Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
        {
            UseShellExecute = true
        });

        Environment.Exit(0);
    }
}
