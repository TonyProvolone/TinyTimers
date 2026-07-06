using System.Diagnostics;
using System.IO;

namespace TinyTimers.Services;

/// <summary>Resolves the human-readable name an app is normally known by - the same one shown
/// hovering its taskbar icon - instead of its raw executable filename. A game's exe name is often
/// generic or bears no resemblance to the game itself (e.g. RocketLeague.exe's internal name is
/// "TAGame"; a differently-named exe can have a completely different FileDescription), so reading
/// the exe's own version resource gets a far more recognizable name than the filename ever would.</summary>
internal static class FriendlyAppName
{
    public static string Resolve(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
                return info.FileDescription.Trim();
            if (!string.IsNullOrWhiteSpace(info.ProductName))
                return info.ProductName.Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File moved, deleted, or otherwise unreadable - fall back to the filename below.
        }

        return Path.GetFileNameWithoutExtension(exePath);
    }
}
