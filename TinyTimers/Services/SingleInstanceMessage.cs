using System.Runtime.InteropServices;

namespace TinyTimers.Services;

internal static class SingleInstanceMessage
{
    public static readonly uint ShowRequest = RegisterWindowMessage("TinyTimers_ShowRequest_9F3E1C2B");

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Finds the running instance's main window and asks it to show itself.
    /// HWND_BROADCAST does not reliably reach hidden/tray-parked windows, so this
    /// targets the specific window handle directly instead.</summary>
    public static void NotifyExistingInstance()
    {
        var hwnd = FindWindow(null, "Tiny Timers");
        if (hwnd != IntPtr.Zero)
            PostMessage(hwnd, ShowRequest, IntPtr.Zero, IntPtr.Zero);
    }
}
