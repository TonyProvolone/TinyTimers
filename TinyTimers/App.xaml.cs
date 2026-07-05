using System.Threading;
using System.Windows;
using System.Windows.Interop;
using TinyTimers.Services;
using Application = System.Windows.Application;

namespace TinyTimers;

public partial class App : Application
{
    public static bool StartMinimized { get; private set; }

    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "TinyTimers-SingleInstance-9F3E1C2B-4A5D-4E6F-8B7A-1C2D3E4F5A6B", out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running (possibly hidden in the tray) - ask it to show
            // itself instead of starting a second instance with its own independent timer state.
            SingleInstanceMessage.NotifyExistingInstance();
            Shutdown();
            return;
        }

        StartMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        if (StartMinimized)
        {
            mainWindow.ShowInTaskbar = false;
            // Force the native window handle to exist (without showing it) so the hidden
            // instance can still receive the show-request message from a later launch attempt.
            _ = new WindowInteropHelper(mainWindow).EnsureHandle();
        }
        else
        {
            mainWindow.Show();
        }
    }
}
