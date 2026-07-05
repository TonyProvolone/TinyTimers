using System.Runtime.InteropServices;

namespace TinyTimers.Services;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4
}

internal static class GlobalHotkey
{
    public const int ToggleId = 0x4A17;
    public const int ResetId = 0x4A18;
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static bool Register(IntPtr hWnd, int id, HotkeyModifiers modifiers, uint virtualKey) =>
        RegisterHotKey(hWnd, id, (uint)modifiers, virtualKey);

    public static void Unregister(IntPtr hWnd, int id) => UnregisterHotKey(hWnd, id);
}
