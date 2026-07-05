using Microsoft.Win32;

namespace TinyTimers.Services;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TinyTimers";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
            return;

        if (enabled)
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName;
            key.SetValue(ValueName, $"\"{exePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
