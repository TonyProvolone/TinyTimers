using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TinyTimers.Models;
using TinyTimers.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace TinyTimers;

public partial class EditTimerDialog : Window
{
    private readonly HashSet<string> _otherSlugs;

    public string NewName { get; private set; } = "";
    public TimeSpan NewDisplayTime { get; private set; }
    public string? LinkedAppPath { get; private set; }
    public string? SoundFilePath { get; private set; }

    public EditTimerDialog(
        string currentName,
        TimerKind kind,
        TimeSpan currentDisplayTime,
        string? currentLinkedAppPath,
        string? currentSoundFilePath,
        IEnumerable<string> otherExistingNames)
    {
        InitializeComponent();
        _otherSlugs = otherExistingNames.Select(TimerFileWriter.Slugify).ToHashSet();

        NameBox.Text = currentName;
        TimeLabel.Text = kind == TimerKind.Countdown ? "Set countdown duration" : "Set current time";
        HoursBox.Text = ((int)currentDisplayTime.TotalHours).ToString();
        MinutesBox.Text = currentDisplayTime.Minutes.ToString("00");
        SecondsBox.Text = currentDisplayTime.Seconds.ToString("00");

        if (kind == TimerKind.Countdown)
        {
            LinkedAppPanel.Visibility = Visibility.Collapsed;

            SoundPanel.Visibility = Visibility.Visible;
            SoundFilePath = currentSoundFilePath;
            UpdateSoundDisplay();
        }
        else
        {
            LinkedAppPath = currentLinkedAppPath;
            UpdateLinkedAppDisplay();
        }

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
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

    private void Save_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void NameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            TryAccept();
    }

    private void TimeBox_KeyDown(object sender, KeyEventArgs e)
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
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "Enter a timer name";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (_otherSlugs.Contains(TimerFileWriter.Slugify(name)))
        {
            ErrorText.Text = "A timer with this name already exists";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        if (!int.TryParse(HoursBox.Text.Trim(), out var hours) || hours < 0 ||
            !int.TryParse(MinutesBox.Text.Trim(), out var minutes) || minutes is < 0 or > 59 ||
            !int.TryParse(SecondsBox.Text.Trim(), out var seconds) || seconds is < 0 or > 59)
        {
            ErrorText.Text = "Enter a valid time (minutes/seconds 0-59)";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        NewName = name;
        NewDisplayTime = new TimeSpan(0, hours, minutes, seconds);
        DialogResult = true;
    }
}
