using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TinyTimers.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TinyTimers;

public partial class SetHotkeyDialog : Window
{
    public HotkeyModifiers NewModifiers { get; private set; }
    public uint NewKey { get; private set; }

    public SetHotkeyDialog(string title, HotkeyModifiers currentModifiers, uint currentKey)
    {
        InitializeComponent();
        Title = title;

        NewModifiers = currentModifiers;
        NewKey = currentKey;
        CaptureBox.Text = FormatHotkey(currentModifiers, KeyInterop.KeyFromVirtualKey((int)currentKey));

        Loaded += (_, _) => CaptureBox.Focus();
    }

    private static string FormatHotkey(HotkeyModifiers modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    private void CaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            ShowError("The Windows key can't be used - it's reserved by Windows itself");
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;

        if (modifiers == HotkeyModifiers.None)
        {
            ShowError("Choose a combination with at least one of Ctrl, Alt, or Shift");
            return;
        }

        NewModifiers = modifiers;
        NewKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        CaptureBox.Text = FormatHotkey(modifiers, key);
        ErrorText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
