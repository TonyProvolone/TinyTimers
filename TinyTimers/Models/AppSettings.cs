using TinyTimers.Services;

namespace TinyTimers.Models;

public sealed class AppSettings
{
    public bool RunOnStartup { get; set; }
    public bool MinimizeToTaskbar { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool AutomaticUpdates { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.System;

    public SizeScale TimerNameSize { get; set; } = SizeScale.Regular;
    public SizeScale TimerValueSize { get; set; } = SizeScale.Regular;
    public SizeScale TimerButtonSize { get; set; } = SizeScale.Regular;

    /// <summary>Null or empty means use the default location under %LocalAppData%.</summary>
    public string? TimerFilesDirectory { get; set; }

    /// <summary>Global hotkey that starts/pauses/resumes whichever linked timer is "active".
    /// Defaults to Ctrl+Alt+G, which isn't a reserved Windows shortcut.</summary>
    public HotkeyModifiers HotkeyModifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    public uint HotkeyKey { get; set; } = 0x47; // VK_G

    /// <summary>Global hotkey that resets whichever linked timer is "active".
    /// Defaults to Ctrl+Alt+R, which isn't a reserved Windows shortcut.</summary>
    public HotkeyModifiers ResetHotkeyModifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    public uint ResetHotkeyKey { get; set; } = 0x52; // VK_R
}
