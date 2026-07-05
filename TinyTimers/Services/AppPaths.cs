using System.IO;

namespace TinyTimers.Services;

internal static class AppPaths
{
    public static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TinyTimers");
}
