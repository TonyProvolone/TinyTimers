using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TinyTimers.Models;
using TinyTimers.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TinyTimers;

public partial class NewTimerDialog : Window
{
    private readonly HashSet<string> _existingSlugs;

    public string? TimerName { get; private set; }
    public TimerKind Kind { get; private set; }
    public TimeSpan CountdownDuration { get; private set; }
    public string? LinkedAppPath { get; private set; }
    public string? SoundFilePath { get; private set; }

    public NewTimerDialog(IEnumerable<string> existingNames)
    {
        InitializeComponent();
        _existingSlugs = existingNames.Select(TimerFileWriter.Slugify).ToHashSet();
        Loaded += (_, _) => TimerNameBox.Focus();
    }

    private void TimerType_Changed(object sender, RoutedEventArgs e)
    {
        if (CountdownDurationPanel is null)
            return;

        var isCountdown = CountdownTypeRadio.IsChecked == true;
        CountdownDurationPanel.Visibility = isCountdown ? Visibility.Visible : Visibility.Collapsed;
        SoundPanel.Visibility = isCountdown ? Visibility.Visible : Visibility.Collapsed;

        // Linking an app is a regular-timer-only feature.
        LinkedAppPanel.Visibility = isCountdown ? Visibility.Collapsed : Visibility.Visible;
        if (isCountdown)
        {
            LinkedAppPath = null;
            UpdateLinkedAppDisplay();
        }
    }

    private void SelectRunningApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SelectRunningAppDialog { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedAppPath is null)
            return;

        LinkedAppPath = dialog.SelectedAppPath;
        UpdateLinkedAppDisplay();
    }

    private void BrowseLinkedApp_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select an app to link to this timer"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        LinkedAppPath = dialog.FileName;
        UpdateLinkedAppDisplay();
    }

    private void ClearLinkedApp_Click(object sender, RoutedEventArgs e)
    {
        LinkedAppPath = null;
        UpdateLinkedAppDisplay();
    }

    private void UpdateLinkedAppDisplay()
    {
        LinkedAppBox.Text = LinkedAppPath is null ? "None selected" : System.IO.Path.GetFileName(LinkedAppPath);
        ClearLinkedAppButton.Visibility = LinkedAppPath is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BrowseSound_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Filter = "Audio files (*.wav;*.mp3;*.wma)|*.wav;*.mp3;*.wma|All files (*.*)|*.*",
            Title = "Select a sound to play when this countdown finishes"
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        SoundFilePath = dialog.FileName;
        UpdateSoundDisplay();
    }

    private void ResetSound_Click(object sender, RoutedEventArgs e)
    {
        SoundFilePath = null;
        UpdateSoundDisplay();
    }

    private void UpdateSoundDisplay()
    {
        SoundBox.Text = SoundFilePath is null ? "Default ding" : System.IO.Path.GetFileName(SoundFilePath);
        ResetSoundButton.Visibility = SoundFilePath is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void TimerNameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            TryAccept();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TryAccept()
    {
        var name = TimerNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Enter a timer name";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (_existingSlugs.Contains(TimerFileWriter.Slugify(name)))
        {
            ErrorText.Text = "A timer with this name already exists";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var kind = CountdownTypeRadio.IsChecked == true ? TimerKind.Countdown : TimerKind.CountUp;
        var duration = TimeSpan.Zero;

        if (kind == TimerKind.Countdown)
        {
            if (!int.TryParse(HoursBox.Text.Trim(), out var hours) || hours < 0 ||
                !int.TryParse(MinutesBox.Text.Trim(), out var minutes) || minutes is < 0 or > 59 ||
                !int.TryParse(SecondsBox.Text.Trim(), out var seconds) || seconds is < 0 or > 59)
            {
                ErrorText.Text = "Enter a valid countdown time (minutes/seconds 0-59)";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            duration = new TimeSpan(0, hours, minutes, seconds);
            if (duration == TimeSpan.Zero)
            {
                ErrorText.Text = "A countdown needs to be longer than zero";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
        }

        TimerName = name;
        Kind = kind;
        CountdownDuration = duration;
        DialogResult = true;
    }
}
