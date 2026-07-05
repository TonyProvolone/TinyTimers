using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TinyTimers.Models;
using TinyTimers.Services;
using Microsoft.Win32;

namespace TinyTimers;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TimerItem> _regularTimers = new();
    private readonly ObservableCollection<TimerItem> _countdownTimers = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _updateCheckTimer = new() { Interval = TimeSpan.FromHours(3) };
    private readonly AppSettings _settings = SettingsStore.Load();
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;
    private FileSystemWatcher? _fileWatcher;
    private bool _isExiting;
    private bool _toggleHotkeyRegistered;
    private bool _resetHotkeyRegistered;
    private Version? _lastNotifiedUpdateVersion;
    private string? _pendingToastUrl;

    /// <summary>The regular timer active_app.txt is currently tracking. Sticky: once a timer
    /// becomes foreground-active it keeps being written every tick even after focus moves
    /// elsewhere (e.g. tabbing over to check OBS), so the file doesn't freeze mid-stream. Only
    /// changes when a *different* regular timer becomes foreground-active.</summary>
    private TimerItem? _activeAppTimer;

    private IEnumerable<TimerItem> AllTimers => _regularTimers.Concat(_countdownTimers);

    public MainWindow()
    {
        ThemeManager.Apply(_settings.Theme);

        InitializeComponent();

        Topmost = _settings.AlwaysOnTop;

        ConfigureTimerFileLocation(_settings.TimerFilesDirectory);

        var records = TimerStore.LoadRecordsForFolder(TimerFileWriter.OutputDirectory);
        ReconcileExternallyRenamedRecords(records);
        foreach (var record in records)
            AddTimerFromRecord(record);

        RewriteFileManifest();
        SetUpFileWatcher();

        RegularTimersList.ItemsSource = _regularTimers;
        CountdownTimersList.ItemsSource = _countdownTimers;

        _regularTimers.CollectionChanged += Timers_CollectionChanged;
        _countdownTimers.CollectionChanged += Timers_CollectionChanged;
        UpdateEmptyState();

        if (_regularTimers.Count > 0)
            TimerFileWriter.EnsureActiveAppFileExists();

        _clock.Tick += (_, _) =>
        {
            var foregroundProcess = ForegroundWindow.GetProcessName();
            foreach (var timer in AllTimers)
            {
                timer.Tick();
                timer.UpdateLinkedAppStatus();
                timer.UpdateForegroundStatus(foregroundProcess);
            }

            var foregroundMatch = _regularTimers.FirstOrDefault(t => t.IsForegroundActive);
            if (foregroundMatch is not null)
                _activeAppTimer = foregroundMatch;

            if (_activeAppTimer is not null)
                TimerFileWriter.WriteActiveApp(_activeAppTimer.Elapsed);

            UpdateTrayTooltip();
        };
        _clock.Start();

        SetUpTrayIcon();

        _updateCheckTimer.Tick += (_, _) => CheckForUpdatesInBackground();
        _updateCheckTimer.Start();
        Dispatcher.BeginInvoke(new Action(CheckForUpdatesInBackground), DispatcherPriority.ApplicationIdle);

        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

        Closing += MainWindow_Closing;
        Closed += (_, _) => System.Windows.Application.Current.Shutdown();
    }

    private void Timers_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateEmptyState();
        UpdateTrayTooltip();
        RewriteFileManifest();

        if (_regularTimers.Count > 0)
            TimerFileWriter.EnsureActiveAppFileExists();
    }

    private void AddTimerFromRecord(TimerRecord record)
    {
        var item = new TimerItem(
            record.Id,
            record.Name,
            record.Kind,
            record.Elapsed,
            record.CountdownDuration,
            record.LinkedAppPath,
            record.SoundFilePath);

        (item.Kind == TimerKind.CountUp ? _regularTimers : _countdownTimers).Add(item);
    }

    private static void ConfigureTimerFileLocation(string? directory)
    {
        TimerFileWriter.Configure(directory);
        KnownFoldersStore.Track(TimerFileWriter.OutputDirectory);
    }

    private void SystemEvents_UserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _settings.Theme != AppTheme.System)
            return;

        Dispatcher.Invoke(() => ThemeManager.Apply(AppTheme.System));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ((HwndSource)PresentationSource.FromVisual(this)).AddHook(WndProc);

        var hwnd = new WindowInteropHelper(this).Handle;
        _toggleHotkeyRegistered = GlobalHotkey.Register(hwnd, GlobalHotkey.ToggleId, _settings.HotkeyModifiers, _settings.HotkeyKey);
        _resetHotkeyRegistered = GlobalHotkey.Register(hwnd, GlobalHotkey.ResetId, _settings.ResetHotkeyModifiers, _settings.ResetHotkeyKey);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)SingleInstanceMessage.ShowRequest)
        {
            RestoreFromTray();
            handled = true;
        }
        else if (msg == GlobalHotkey.WM_HOTKEY && wParam.ToInt32() == GlobalHotkey.ToggleId)
        {
            ResolveActiveTimer()?.ToggleOrReset();
            handled = true;
        }
        else if (msg == GlobalHotkey.WM_HOTKEY && wParam.ToInt32() == GlobalHotkey.ResetId)
        {
            ResolveActiveTimer()?.Reset();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>Resolves which linked timer the global hotkeys should act on.
    ///
    /// A user could link several timers to different apps that all happen to be open at once,
    /// which would make more than one timer "active" at the same time. To disambiguate:
    ///   1. If exactly one timer has a linked app configured at all, always use that one - it works
    ///      from anywhere, since there's no other candidate it could mean.
    ///   2. Otherwise, only act on whichever linked app is currently in the foreground (the game/app
    ///      the user is actually looking at right now).</summary>
    private TimerItem? ResolveActiveTimer()
    {
        var linkedTimers = AllTimers.Where(t => t.HasLinkedApp).ToList();
        if (linkedTimers.Count == 1)
            return linkedTimers[0];

        var foregroundProcess = ForegroundWindow.GetProcessName();
        return string.IsNullOrEmpty(foregroundProcess)
            ? null
            : linkedTimers.FirstOrDefault(t => string.Equals(t.LinkedAppProcessName, foregroundProcess, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Best-effort recovery for a timer file that was renamed outside the app while it was closed.
    /// Only acts when there's exactly one missing timer and exactly one unclaimed file, since that's the
    /// only case where the match is unambiguous. Must run on the raw records, before any TimerItem is
    /// constructed - a TimerItem writes its file immediately on construction, which would make the
    /// "missing" file reappear before this check ever saw the gap.</summary>
    private static void ReconcileExternallyRenamedRecords(List<TimerRecord> records)
    {
        if (!Directory.Exists(TimerFileWriter.OutputDirectory))
            return;

        var expectedPaths = new HashSet<string>(records.Select(r => TimerFileWriter.GetFilePath(r.Name)), StringComparer.OrdinalIgnoreCase);
        var missingRecords = records.Where(r => !File.Exists(TimerFileWriter.GetFilePath(r.Name))).ToList();
        if (missingRecords.Count != 1)
            return;

        var unclaimedFiles = Directory.EnumerateFiles(TimerFileWriter.OutputDirectory, "*.txt")
            .Where(f => !expectedPaths.Contains(f))
            .ToList();

        if (unclaimedFiles.Count != 1)
            return;

        missingRecords[0].Name = TimerFileWriter.NameFromFilePath(unclaimedFiles[0]);
    }

    private void SetUpFileWatcher()
    {
        _fileWatcher?.Dispose();

        Directory.CreateDirectory(TimerFileWriter.OutputDirectory);
        _fileWatcher = new FileSystemWatcher(TimerFileWriter.OutputDirectory, "*.txt")
        {
            NotifyFilter = NotifyFilters.FileName
        };
        _fileWatcher.Renamed += OnTimerFileRenamed;
        _fileWatcher.EnableRaisingEvents = true;
    }

    private void OnTimerFileRenamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var timer = AllTimers.FirstOrDefault(t => string.Equals(t.FilePath, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
            if (timer is null)
                return;

            var newName = TimerFileWriter.NameFromFilePath(e.FullPath);
            timer.AdoptExternalRename(newName);
            RewriteFileManifest();
        });
    }

    /// <summary>Rebuilds the manifest of every timer file the app currently considers "in use",
    /// across every folder profile it's ever saved - not just the currently active one - so
    /// uninstall cleanup can find files left behind in folders the user isn't using right now.</summary>
    private void RewriteFileManifest() =>
        TimerFileWriter.WriteManifest(BuildAllKnownFilePaths().Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>Every timer and active-app file path the app currently considers legitimately in
    /// use, across every folder profile it's ever saved - not just the currently active one. Used
    /// both for the uninstall manifest and as the "don't delete these" set for Clear Old Timers,
    /// so switching away from a folder doesn't make its still-valid files look orphaned.</summary>
    private List<string> BuildAllKnownFilePaths()
    {
        var paths = new List<string>();

        foreach (var profile in TimerStore.LoadAllProfiles())
        {
            if (IsCurrentFolder(profile.FolderPath))
                continue; // the current folder's live paths come from AllTimers below, which may include not-yet-saved edits

            foreach (var record in profile.Timers)
                paths.Add(TimerFileWriter.GetFilePathIn(profile.FolderPath, record.Name));

            if (profile.Timers.Any(r => r.Kind == TimerKind.CountUp))
                paths.Add(TimerFileWriter.GetActiveAppFilePathIn(profile.FolderPath));
        }

        paths.AddRange(AllTimers.Select(t => t.FilePath));

        if (_regularTimers.Count > 0)
            paths.Add(TimerFileWriter.ActiveAppFilePath);

        return paths;
    }

    private static bool IsCurrentFolder(string folderPath) =>
        string.Equals(
            Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(TimerFileWriter.OutputDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private void SetUpTrayIcon()
    {
        var exePath = Environment.ProcessPath;
        var icon = exePath is not null
            ? System.Drawing.Icon.ExtractAssociatedIcon(exePath)
            : null;

        _trayMenu = new System.Windows.Forms.ContextMenuStrip();
        _trayMenu.Opening += (_, _) => BuildTrayMenu();

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Text = "Tiny Timers",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.BalloonTipClicked += (_, _) =>
        {
            if (_pendingToastUrl is { } url)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        };

        UpdateTrayTooltip();
    }

    /// <summary>Best-effort check against the GitHub releases API; failures (offline, rate limiting,
    /// etc.) are silently ignored since this runs unattended in the background. Shows a tray balloon
    /// (which doesn't steal focus from whatever app the user is currently in) either to let them know
    /// an update exists, or - when automatic updates are enabled - once it's been downloaded and is
    /// ready to install on next restart.</summary>
    private async void CheckForUpdatesInBackground()
    {
        try
        {
            var info = await UpdateChecker.GetLatestReleaseAsync();
            if (info is null || !UpdateChecker.IsNewer(info.Version) || info.Version == _lastNotifiedUpdateVersion)
                return;

            if (_settings.AutomaticUpdates)
            {
                if (!await UpdateInstaller.DownloadAsync(info))
                    return;

                _lastNotifiedUpdateVersion = info.Version;
                ShowUpdateToast("Tiny Timers update ready", $"{info.TagName} was downloaded and will install next time you restart Tiny Timers.", info.HtmlUrl);
            }
            else
            {
                _lastNotifiedUpdateVersion = info.Version;
                ShowUpdateToast("Tiny Timers update available", $"{info.TagName} is available. Click to view the release.", info.HtmlUrl);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException or UnauthorizedAccessException)
        {
        }
    }

    private void ShowUpdateToast(string title, string text, string clickUrl)
    {
        if (_trayIcon is null)
            return;

        _pendingToastUrl = clickUrl;
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.ShowBalloonTip(10000);
    }

    private void BuildTrayMenu()
    {
        if (_trayMenu is null)
            return;

        _trayMenu.Items.Clear();

        var timerList = AllTimers.ToList();
        if (timerList.Count == 0)
        {
            _trayMenu.Items.Add(new System.Windows.Forms.ToolStripMenuItem("No timers yet") { Enabled = false });
        }
        else
        {
            for (var i = 0; i < timerList.Count; i++)
            {
                var timer = timerList[i];
                var item = new System.Windows.Forms.ToolStripMenuItem($"{timer.Name} — {timer.Elapsed} ({timer.ToggleLabel})");
                item.Click += (_, _) => timer.ToggleOrReset();
                _trayMenu.Items.Add(item);

                // Only separate timers from each other when there's more than one to tell apart.
                if (timerList.Count > 1 && i < timerList.Count - 1)
                    _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            }
        }

        _trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open");
        openItem.Click += (_, _) => RestoreFromTray();
        _trayMenu.Items.Add(openItem);

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        _trayMenu.Items.Add(exitItem);
    }

    private void UpdateTrayTooltip()
    {
        if (_trayIcon is null)
            return;

        var timerList = AllTimers.ToList();
        if (timerList.Count == 0)
        {
            _trayIcon.Text = "Tiny Timers";
            return;
        }

        const int maxLength = 120;
        const string separator = "\n────────────";
        var sb = new StringBuilder("Tiny Timers");

        for (var i = 0; i < timerList.Count; i++)
        {
            var timer = timerList[i];
            var line = $"\n{timer.Name}: {timer.Elapsed}";
            if (sb.Length + line.Length > maxLength)
            {
                sb.Append("\n...");
                break;
            }

            sb.Append(line);

            // Only separate timers from each other when there's more than one to tell apart.
            if (timerList.Count > 1 && i < timerList.Count - 1 && sb.Length + separator.Length <= maxLength)
                sb.Append(separator);
        }

        _trayIcon.Text = sb.ToString();
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting && _settings.MinimizeToTaskbar)
        {
            e.Cancel = true;
            Hide();
            ShowInTaskbar = false;
            return;
        }

        _clock.Stop();
        _updateCheckTimer.Stop();

        var allTimers = AllTimers.ToList();
        foreach (var timer in allTimers)
        {
            if (timer.IsRunning)
                timer.Toggle();
        }

        TimerStore.SaveRecordsForFolder(TimerFileWriter.OutputDirectory, allTimers);
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (_toggleHotkeyRegistered)
            GlobalHotkey.Unregister(hwnd, GlobalHotkey.ToggleId);
        if (_resetHotkeyRegistered)
            GlobalHotkey.Unregister(hwnd, GlobalHotkey.ResetId);

        _fileWatcher?.Dispose();
        _trayIcon?.Dispose();
    }

    private void UpdateEmptyState()
    {
        RegularSection.Visibility = _regularTimers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        CountdownSection.Visibility = _countdownTimers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateText.Visibility = _regularTimers.Count == 0 && _countdownTimers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddTimerButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewTimerDialog(AllTimers.Select(t => t.Name)) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.TimerName is not { } name)
            return;

        var item = new TimerItem(
            name,
            dialog.Kind,
            countdownDuration: dialog.CountdownDuration,
            linkedAppPath: dialog.LinkedAppPath,
            soundFilePath: dialog.SoundFilePath);

        (item.Kind == TimerKind.CountUp ? _regularTimers : _countdownTimers).Add(item);
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is TimerItem item)
            item.ToggleOrReset();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not TimerItem item)
            return;

        var otherNames = AllTimers.Where(t => t != item).Select(t => t.Name);
        var currentTime = item.Kind == TimerKind.Countdown ? item.CountdownDuration : item.DisplayTime;
        var dialog = new EditTimerDialog(item.Name, item.Kind, currentTime, item.LinkedAppPath, item.SoundFilePath, otherNames) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            item.Rename(dialog.NewName);
            item.SetDisplayTime(dialog.NewDisplayTime);
            item.SetLinkedApp(dialog.LinkedAppPath);
            item.SetSound(dialog.SoundFilePath);
            RewriteFileManifest();
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not TimerItem item)
            return;

        var resetTarget = item.Kind == TimerKind.Countdown ? item.CountdownDuration : TimeSpan.Zero;
        var dialog = new ConfirmDialog(
            "Reset Timer",
            $"Reset \"{item.Name}\" back to {TimerItem.FormatTimeSpan(resetTarget)}?",
            "Reset",
            isDestructive: true) { Owner = this };

        if (dialog.ShowDialog() == true)
            item.Reset();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not TimerItem item)
            return;

        var dialog = new ConfirmDialog(
            "Delete Timer",
            $"Delete \"{item.Name}\"? This cannot be undone.",
            "Delete",
            isDestructive: true) { Owner = this };

        if (dialog.ShowDialog() != true)
            return;

        item.DeleteFile();
        (item.Kind == TimerKind.CountUp ? _regularTimers : _countdownTimers).Remove(item);

        if (_activeAppTimer == item)
            _activeAppTimer = null;
    }

    private void RevealFile_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not TimerItem item)
            return;

        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
    }

    private void OptionsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OptionsDialog(_settings, BuildAllKnownFilePaths()) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        _settings.RunOnStartup = dialog.RunOnStartup;
        _settings.MinimizeToTaskbar = dialog.MinimizeToTaskbar;
        _settings.AutomaticUpdates = dialog.AutomaticUpdates;

        if (dialog.AlwaysOnTop != _settings.AlwaysOnTop)
        {
            _settings.AlwaysOnTop = dialog.AlwaysOnTop;
            Topmost = _settings.AlwaysOnTop;
        }

        if (dialog.Theme != _settings.Theme)
        {
            _settings.Theme = dialog.Theme;
            ThemeManager.Apply(_settings.Theme);
        }

        if (dialog.HotkeyModifiers != _settings.HotkeyModifiers || dialog.HotkeyKey != _settings.HotkeyKey)
            ApplyHotkeyChange(GlobalHotkey.ToggleId, dialog.HotkeyModifiers, dialog.HotkeyKey);

        if (dialog.ResetHotkeyModifiers != _settings.ResetHotkeyModifiers || dialog.ResetHotkeyKey != _settings.ResetHotkeyKey)
            ApplyHotkeyChange(GlobalHotkey.ResetId, dialog.ResetHotkeyModifiers, dialog.ResetHotkeyKey);

        if (dialog.TimerFilesDirectory != _settings.TimerFilesDirectory)
            SwitchTimerFolder(dialog.TimerFilesDirectory);

        SettingsStore.Save(_settings);
        StartupManager.SetEnabled(_settings.RunOnStartup);
    }

    private void ApplyHotkeyChange(int id, HotkeyModifiers modifiers, uint key)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        ref var registered = ref (id == GlobalHotkey.ToggleId ? ref _toggleHotkeyRegistered : ref _resetHotkeyRegistered);
        var previousModifiers = id == GlobalHotkey.ToggleId ? _settings.HotkeyModifiers : _settings.ResetHotkeyModifiers;
        var previousKey = id == GlobalHotkey.ToggleId ? _settings.HotkeyKey : _settings.ResetHotkeyKey;

        if (registered)
            GlobalHotkey.Unregister(hwnd, id);

        registered = GlobalHotkey.Register(hwnd, id, modifiers, key);

        if (registered)
        {
            if (id == GlobalHotkey.ToggleId)
            {
                _settings.HotkeyModifiers = modifiers;
                _settings.HotkeyKey = key;
            }
            else
            {
                _settings.ResetHotkeyModifiers = modifiers;
                _settings.ResetHotkeyKey = key;
            }
        }
        else
        {
            // Couldn't register the new combo (likely already claimed by another app) - fall
            // back to re-registering whatever was working before, and let the user know.
            registered = GlobalHotkey.Register(hwnd, id, previousModifiers, previousKey);

            new ConfirmDialog(
                "Hotkey Unavailable",
                "That key combination is already in use by another app or by Windows. Your previous hotkey is still active - try a different combination.",
                "OK",
                showCancel: false) { Owner = this }.ShowDialog();
        }
    }

    /// <summary>Switches to a different timer-files folder, treating each folder as its own
    /// independent profile: the current folder's timers are saved as-is (so switching back
    /// later restores them exactly), then whatever was previously saved for the destination
    /// folder is loaded in - or an empty set, if it's never been used before.</summary>
    private void SwitchTimerFolder(string? newDirectory)
    {
        TimerStore.SaveRecordsForFolder(TimerFileWriter.OutputDirectory, AllTimers);

        _regularTimers.Clear();
        _countdownTimers.Clear();
        _activeAppTimer = null;

        ConfigureTimerFileLocation(newDirectory);
        _settings.TimerFilesDirectory = newDirectory;

        var records = TimerStore.LoadRecordsForFolder(TimerFileWriter.OutputDirectory);
        ReconcileExternallyRenamedRecords(records);
        foreach (var record in records)
            AddTimerFromRecord(record);

        RewriteFileManifest();
        SetUpFileWatcher();
    }
}
