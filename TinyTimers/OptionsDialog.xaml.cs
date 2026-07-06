using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    public bool AutomaticUpdates { get; private set; }
    public AppTheme Theme { get; private set; }
    public SizeScale TimerNameSize { get; private set; }
    public SizeScale TimerValueSize { get; private set; }
    public SizeScale TimerButtonSize { get; private set; }
    public string? TimerFilesDirectory { get; private set; }
    public HotkeyModifiers HotkeyModifiers { get; private set; }
    public uint HotkeyKey { get; private set; }
    public HotkeyModifiers ResetHotkeyModifiers { get; private set; }
    public uint ResetHotkeyKey { get; private set; }

    private string? _latestReleaseHtmlUrl;

    public OptionsDialog(AppSettings current, IReadOnlyCollection<string> activeTimerFilePaths)
    {
        InitializeComponent();
        _activeTimerFilePaths = activeTimerFilePaths;

        RunOnStartupCheck.IsChecked = current.RunOnStartup;
        MinimizeToTaskbarCheck.IsChecked = current.MinimizeToTaskbar;
        AlwaysOnTopCheck.IsChecked = current.AlwaysOnTop;
        AutomaticUpdatesCheck.IsChecked = current.AutomaticUpdates;
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

        SetSizeRadio(current.TimerNameSize, NameSizeRegularRadio, NameSizeLargeRadio, NameSizeGiantRadio);
        SetSizeRadio(current.TimerValueSize, ValueSizeRegularRadio, ValueSizeLargeRadio, ValueSizeGiantRadio);
        SetSizeRadio(current.TimerButtonSize, ButtonSizeRegularRadio, ButtonSizeLargeRadio, ButtonSizeGiantRadio);

        UpdatePathDisplay();
        UpdateHotkeyDisplay();
        UpdateResetHotkeyDisplay();
        CurrentVersionText.Text = $"You're on version {UpdateChecker.CurrentVersion.ToString(3)}";
    }

    private static void SetSizeRadio(
        SizeScale value,
        System.Windows.Controls.RadioButton regular,
        System.Windows.Controls.RadioButton large,
        System.Windows.Controls.RadioButton giant)
    {
        switch (value)
        {
            case SizeScale.Large:
                large.IsChecked = true;
                break;
            case SizeScale.Giant:
                giant.IsChecked = true;
                break;
            default:
                regular.IsChecked = true;
                break;
        }
    }

    private static SizeScale GetSizeRadio(System.Windows.Controls.RadioButton large, System.Windows.Controls.RadioButton giant) =>
        large.IsChecked == true ? SizeScale.Large
        : giant.IsChecked == true ? SizeScale.Giant
        : SizeScale.Regular;

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

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking for updates...";
        _latestReleaseHtmlUrl = null;

        try
        {
            var info = await UpdateChecker.GetLatestReleaseAsync();
            if (info is null)
            {
                UpdateStatusText.Text = "Couldn't check for updates. Try again later.";
                return;
            }

            if (!UpdateChecker.IsNewer(info.Version))
            {
                UpdateStatusText.Text = $"You're up to date ({UpdateChecker.CurrentVersion.ToString(3)}).";
                return;
            }

            _latestReleaseHtmlUrl = info.HtmlUrl;

            if (AutomaticUpdatesCheck.IsChecked == true)
            {
                UpdateStatusText.Text = $"Downloading {info.TagName}...";
                var downloaded = await UpdateInstaller.DownloadAsync(info);
                UpdateStatusText.Text = downloaded
                    ? $"{info.TagName} downloaded — it'll install next time you restart Tiny Timers."
                    : $"{info.TagName} is available. Click to view the release.";
            }
            else
            {
                UpdateStatusText.Text = $"{info.TagName} is available. Click to view the release.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException or UnauthorizedAccessException)
        {
            UpdateStatusText.Text = "Couldn't check for updates. Try again later.";
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void UpdateStatusText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_latestReleaseHtmlUrl is { } url)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        RunOnStartup = RunOnStartupCheck.IsChecked == true;
        MinimizeToTaskbar = MinimizeToTaskbarCheck.IsChecked == true;
        AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
        AutomaticUpdates = AutomaticUpdatesCheck.IsChecked == true;
        Theme = LightThemeRadio.IsChecked == true ? AppTheme.Light
            : DarkThemeRadio.IsChecked == true ? AppTheme.Dark
            : AppTheme.System;

        TimerNameSize = GetSizeRadio(NameSizeLargeRadio, NameSizeGiantRadio);
        TimerValueSize = GetSizeRadio(ValueSizeLargeRadio, ValueSizeGiantRadio);
        TimerButtonSize = GetSizeRadio(ButtonSizeLargeRadio, ButtonSizeGiantRadio);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
