using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using TinyTimers.Services;

namespace TinyTimers.Models;

public sealed class TimerItem : INotifyPropertyChanged
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _offset = TimeSpan.Zero;
    private System.Windows.Media.MediaPlayer? _mediaPlayer;

    /// <summary>The duration actually driving the current countdown run. Only synced from
    /// CountdownDuration at construction and on Reset() - deliberately not touched by editing,
    /// so editing a countdown that's mid-run doesn't disturb what it's currently counting down
    /// from. Meaningless when Kind is CountUp.</summary>
    private TimeSpan _activeCountdownDuration;

    public TimerItem(
        string name,
        TimerKind kind,
        TimeSpan initialElapsed = default,
        TimeSpan countdownDuration = default,
        string? linkedAppPath = null,
        string? soundFilePath = null)
        : this(Guid.NewGuid(), name, kind, initialElapsed, countdownDuration, linkedAppPath, soundFilePath)
    {
    }

    public TimerItem(
        Guid id,
        string name,
        TimerKind kind,
        TimeSpan initialElapsed,
        TimeSpan countdownDuration,
        string? linkedAppPath,
        string? soundFilePath = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        CountdownDuration = countdownDuration;
        _activeCountdownDuration = countdownDuration;
        // Linking an app is a regular-timer-only feature - drop it for a countdown even if
        // it's present in old saved data from before this restriction existed.
        LinkedAppPath = kind == TimerKind.Countdown || string.IsNullOrWhiteSpace(linkedAppPath) ? null : linkedAppPath;
        SoundFilePath = string.IsNullOrWhiteSpace(soundFilePath) ? null : soundFilePath;
        _offset = initialElapsed;
        WriteToFile();
    }

    public Guid Id { get; }

    public string Name { get; private set; }

    public TimerKind Kind { get; }

    /// <summary>Only meaningful when Kind is Countdown - the duration counted down from.</summary>
    public TimeSpan CountdownDuration { get; private set; }

    public string? LinkedAppPath { get; private set; }

    public bool HasLinkedApp => LinkedAppPath is not null;

    public string LinkedAppTooltip => HasLinkedApp ? $"Linked app: {Path.GetFileName(LinkedAppPath)}" : "";

    public bool IsLinkedAppRunning { get; private set; }

    /// <summary>True when this timer's linked app is the current foreground window - i.e.
    /// the one the global hotkeys would currently act on. Updated periodically.</summary>
    public bool IsForegroundActive { get; private set; }

    /// <summary>Only meaningful when Kind is Countdown - null means play the default sound.</summary>
    public string? SoundFilePath { get; private set; }

    public bool IsRunning => _stopwatch.IsRunning;

    /// <summary>True when a countdown has run all the way down to zero and is stopped. There's
    /// nothing meaningful left to "resume" at that point, so the UI offers a reset instead.</summary>
    public bool IsFinished =>
        Kind == TimerKind.Countdown && !IsRunning && _activeCountdownDuration > TimeSpan.Zero && CurrentElapsed >= _activeCountdownDuration;

    public string ToggleLabel
    {
        get
        {
            if (IsRunning)
                return "Pause";

            if (IsFinished)
                return "Reset";

            return CurrentElapsed == TimeSpan.Zero ? "Start" : "Resume";
        }
    }

    /// <summary>Time actually spent running, regardless of Kind.</summary>
    public TimeSpan CurrentElapsed => _offset + _stopwatch.Elapsed;

    /// <summary>What should be shown/written for this timer: elapsed time for a count-up
    /// timer, remaining time for a countdown.</summary>
    public TimeSpan DisplayTime
    {
        get
        {
            if (Kind != TimerKind.Countdown)
                return CurrentElapsed;

            var remaining = _activeCountdownDuration - CurrentElapsed;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public string Elapsed => FormatTimeSpan(DisplayTime);

    public string FilePath => TimerFileWriter.GetFilePath(Name);

    /// <summary>Full path to the process this timer's linked app targets, if any and if it's currently running.</summary>
    public string? LinkedAppProcessName => LinkedAppPath is null ? null : Path.GetFileNameWithoutExtension(LinkedAppPath);

    public void Toggle()
    {
        if (_stopwatch.IsRunning)
            _stopwatch.Stop();
        else
            _stopwatch.Start();

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ToggleLabel));
        WriteToFile();
    }

    /// <summary>Starts, pauses, or resumes - except for a finished countdown, where resuming
    /// wouldn't do anything meaningful (it would just immediately finish again), so this resets
    /// it back to its starting point instead. Used wherever ToggleLabel is shown as the action.</summary>
    public void ToggleOrReset()
    {
        if (IsFinished)
            Reset();
        else
            Toggle();
    }

    public void Rename(string newName)
    {
        if (newName == Name)
            return;

        TimerFileWriter.Delete(Name);
        Name = newName;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FilePath));
        WriteToFile();
    }

    /// <summary>Adopts a name change that happened outside the app (the backing file was renamed on disk).
    /// Unlike Rename(), the file already exists under the new name, so nothing needs to be deleted.</summary>
    public void AdoptExternalRename(string newName)
    {
        Name = newName;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(FilePath));
        WriteToFile();
    }

    /// <summary>Linking an app is a regular-timer-only feature - a no-op for a countdown.</summary>
    public void SetLinkedApp(string? path)
    {
        if (Kind == TimerKind.Countdown)
            return;

        LinkedAppPath = string.IsNullOrWhiteSpace(path) ? null : path;
        OnPropertyChanged(nameof(LinkedAppPath));
        OnPropertyChanged(nameof(HasLinkedApp));
        OnPropertyChanged(nameof(LinkedAppTooltip));
        UpdateLinkedAppStatus();
    }

    public void SetSound(string? path)
    {
        SoundFilePath = string.IsNullOrWhiteSpace(path) ? null : path;
    }

    /// <summary>Re-checks whether the linked app is currently running. Call periodically.</summary>
    public void UpdateLinkedAppStatus()
    {
        var running = false;

        if (LinkedAppProcessName is { } processName)
            running = Process.GetProcessesByName(processName).Length > 0;

        if (running == IsLinkedAppRunning)
            return;

        IsLinkedAppRunning = running;
        OnPropertyChanged(nameof(IsLinkedAppRunning));
    }

    /// <summary>Re-checks whether this timer's linked app is the current foreground window. Call periodically.</summary>
    public void UpdateForegroundStatus(string? foregroundProcessName)
    {
        var isActive = HasLinkedApp
            && !string.IsNullOrEmpty(foregroundProcessName)
            && string.Equals(LinkedAppProcessName, foregroundProcessName, StringComparison.OrdinalIgnoreCase);

        if (isActive == IsForegroundActive)
            return;

        IsForegroundActive = isActive;
        OnPropertyChanged(nameof(IsForegroundActive));
    }

    /// <summary>Resets to the starting point: zero elapsed for a count-up timer, or the
    /// full duration remaining for a countdown. Adopts whatever CountdownDuration currently
    /// is - including any edit made while this timer was running - as the new active duration.</summary>
    public void Reset()
    {
        _stopwatch.Reset();
        _offset = TimeSpan.Zero;
        _activeCountdownDuration = CountdownDuration;

        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(Elapsed));
        WriteToFile();
    }

    /// <summary>Applies an edited time. For a count-up timer this immediately changes the
    /// current elapsed time. For a countdown, this only updates CountdownDuration - the value
    /// Reset() will use next - without touching whatever is currently counting down, so editing
    /// a countdown that's mid-run (or already finished) doesn't yank its current display around.</summary>
    public void SetDisplayTime(TimeSpan displayValue)
    {
        if (Kind == TimerKind.Countdown)
        {
            CountdownDuration = displayValue;
            OnPropertyChanged(nameof(CountdownDuration));
            return;
        }

        var wasRunning = _stopwatch.IsRunning;
        _stopwatch.Reset();
        _offset = displayValue;

        if (wasRunning)
            _stopwatch.Start();

        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ToggleLabel));
        WriteToFile();
    }

    public void Tick()
    {
        var wasRunning = IsRunning;

        if (Kind == TimerKind.Countdown && wasRunning && CurrentElapsed >= _activeCountdownDuration)
        {
            _stopwatch.Stop();
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(ToggleLabel));
            PlayCompletionSound();
        }

        OnPropertyChanged(nameof(Elapsed));

        if (wasRunning)
            WriteToFile();
    }

    private void PlayCompletionSound()
    {
        try
        {
            if (SoundFilePath is not null && File.Exists(SoundFilePath))
            {
                _mediaPlayer ??= new System.Windows.Media.MediaPlayer();
                _mediaPlayer.Open(new Uri(SoundFilePath));
                _mediaPlayer.Play();
            }
            else
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void DeleteFile() => TimerFileWriter.Delete(Name);

    /// <summary>Rewrites this timer's file at whatever directory TimerFileWriter currently points to.</summary>
    public void RefreshFile() => WriteToFile();

    private void WriteToFile() => TimerFileWriter.Write(Name, Elapsed);

    /// <summary>Formats a duration as hh:mm:ss with an uncapped hour count (e.g. 100:00:00),
    /// since a timer can easily run for hundreds of hours.</summary>
    public static string FormatTimeSpan(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
