using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace TinyTimers;

public partial class SelectRunningAppDialog : Window
{
    public string? SelectedAppPath { get; private set; }

    public SelectRunningAppDialog()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => RefreshList();

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void RefreshList()
    {
        var apps = new List<RunningApp>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    continue;

                var path = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
                    continue;

                apps.Add(new RunningApp($"{process.MainWindowTitle} ({process.ProcessName}.exe)", path));
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                // Some processes (elevated, protected, etc.) can't be inspected - just skip them.
            }
        }

        AppList.ItemsSource = apps.OrderBy(a => a.Display).ToList();
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryAccept();

    private void Select_Click(object sender, RoutedEventArgs e) => TryAccept();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TryAccept()
    {
        if (AppList.SelectedItem is not RunningApp app)
            return;

        SelectedAppPath = app.Path;
        DialogResult = true;
    }

    private sealed record RunningApp(string Display, string Path);
}
