using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TinyTimers.Services;

internal static class ForegroundWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>Name (without .exe) of the process that owns the current foreground window, if any.</summary>
    public static string? GetProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
