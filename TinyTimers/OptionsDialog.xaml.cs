using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TinyTimers.Models;
using TinyTimers.Services;

namespace TinyTimers;

public partial class OptionsDialog : Window
{
    private readonly IReadOnlyCollection<string> _activeTimerFilePaths;

    public bool RunOnStartup { get; private set; }
    public bool MinimizeToTaskbar { get; private set; }
    public bool AlwaysOnTop { get; private set; }
    public AppTheme Theme { get; private set; }
    public string? TimerFilesDirectory { get; private set; }
    public HotkeyModifiers HotkeyModifiers { get; private set; }
    public uint HotkeyKey { get; private set; }
    public HotkeyModifiers ResetHotkeyModifiers { get; private set; }
    public uint ResetHotkeyKey { get; private set; }

    public OptionsDialog(AppSettings current, IReadOnlyCollection<string> activeTimerFilePaths)
    {
        InitializeComponent();
        _activeTimerFilePaths = activeTimerFilePaths;

        RunOnStartupCheck.IsChecked = current.RunOnStartup;
        MinimizeToTaskbarCheck.IsChecked = current.MinimizeToTaskbar;
        AlwaysOnTopCheck.IsChecked = current.AlwaysOnTop;
        TimerFilesDirectory = current.TimerFilesDirectory;
        HotkeyModifiers = current.HotkeyModifiers;
        HotkeyKey = current.HotkeyKey;
        ResetHotkeyModifiers = current.ResetHotkeyModifiers;
        ResetHotkeyKey = current.ResetHotkeyKey;

        switch (current.Theme)
        {
            case AppTheme.Dark:
                DarkThemeRadio.IsChecked = true;
                break;
            case AppTheme.Light:
                LightThemeRadio.IsChecked = true;
                break;
            default:
                SystemThemeRadio.IsChecked = true;
                break;
        }

        UpdatePathDisplay();
        UpdateHotkeyDisplay();
        UpdateResetHotkeyDisplay();
    }

    private void UpdateHotkeyDisplay() => HotkeyBox.Text = FormatHotkey(HotkeyModifiers, HotkeyKey);

    private void UpdateResetHotkeyDisplay() => ResetHotkeyBox.Text = FormatHotkey(ResetHotkeyModifiers, ResetHotkeyKey);

    private static string FormatHotkey(HotkeyModifiers modifiers, uint virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey((int)virtualKey);
        var parts = new List<string>();
        if (modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SetHotkeyDialog("Set Play / Pause / Resume Hotkey", HotkeyModifiers, HotkeyKey) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        HotkeyModifiers = dialog.NewModifiers;
        HotkeyKey = dialog.NewKey;
        UpdateHotkeyDisplay();
    }

    private void ChangeResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SetHotkeyDialog("Set Reset Active Timer Hotkey", ResetHotkeyModifiers, ResetHotkeyKey) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        ResetHotkeyModifiers = dialog.NewModifiers;
        ResetHotkeyKey = dialog.NewKey;
        UpdateResetHotkeyDisplay();
    }

    private void UpdatePathDisplay()
    {
        TimerFilesPathBox.Text = string.IsNullOrWhiteSpace(TimerFilesDirectory)
            ? TimerFileWriter.DefaultDirectory
            : TimerFilesDirectory;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where timer text files are stored",
            SelectedPath = TimerFilesPathBox.Text,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TimerFilesDirectory = dialog.SelectedPath;
            UpdatePathDisplay();
        }
    }

    private void ResetLocation_Click(object sender, RoutedEventArgs e)
    {
        TimerFilesDirectory = null;
        UpdatePathDisplay();
    }

    private void ClearOldTimers_Click(object sender, RoutedEventArgs e)
    {
        var confirm = new ConfirmDialog(
            "Clear Old Timers",
            "This deletes any file in your timer folder - and any folder you've used before - that no longer matches one of your existing timers. This cannot be undone.",
            "Clear",
            isDestructive: true) { Owner = this };

        if (confirm.ShowDialog() != true)
            return;

        var folders = new HashSet<string>(KnownFoldersStore.Load(), StringComparer.OrdinalIgnoreCase)
        {
            TimerFileWriter.OutputDirectory
        };

        var removed = 0;
        foreach (var folder in folders)
            removed += TimerFileWriter.RemoveOrphanedFiles(folder, _activeTimerFilePaths);

        var resultMessage = removed switch
        {
            0 => "No old timer files were found.",
            1 => "Removed 1 old timer file.",
            _ => $"Removed {removed} old timer files."
        };

        new ConfirmDialog("Clear Old Timers", resultMessage, "OK", showCancel: false) { Owner = this }.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RunOnStartup = RunOnStartupCheck.IsChecked == true;
        MinimizeToTaskbar = MinimizeToTaskbarCheck.IsChecked == true;
        AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        Theme = LightThemeRadio.IsChecked == true ? AppTheme.Light
            : DarkThemeRadio.IsChecked == true ? AppTheme.Dark
            : AppTheme.System;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
