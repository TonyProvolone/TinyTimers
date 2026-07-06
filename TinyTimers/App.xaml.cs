using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using TinyTimers.Services;
using Application = System.Windows.Application;

namespace TinyTimers;

public partial class App : Application
{
    public static bool StartMinimized { get; private set; }

    private const string SingleInstanceMutexName = "TinyTimers-SingleInstance-9F3E1C2B-4A5D-4E6F-8B7A-1C2D3E4F5A6B";

    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // When launched by an in-app Restart, the previous instance is still shutting down and
        // still holds the single-instance mutex - wait for it to fully exit first, or the check
        // below would treat us as a duplicate and immediately bow out, turning restart into quit.
        var isRestart = WaitForRestartPredecessor(e.Args);

        if (!TryAcquireSingleInstance(isRestart))
        {
            // Another instance is already running (possibly hidden in the tray) - ask it to show
            // itself instead of starting a second instance with its own independent timer state.
            SingleInstanceMessage.NotifyExistingInstance();
            Shutdown();
            return;
        }

        // A previous session may have downloaded an update in the background and deferred
        // installing it until the app was restarted - this is that restart. Run the installer
        // unattended and exit immediately so it can overwrite this exe; it relaunches the app
        // itself once done. If the pending version turns out to be stale (e.g. already applied),
        // just clear it instead.
        if (UpdateInstaller.TryGetPendingInstaller(out var pendingInstallerPath, out var pendingVersionText))
        {
            if (Version.TryParse(pendingVersionText, out var pendingVersion) && UpdateChecker.IsNewer(pendingVersion))
            {
                UpdateInstaller.LaunchInstallerAndExit(pendingInstallerPath);
                return;
            }

            UpdateInstaller.ClearPending();
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

    /// <summary>Waits for the predecessor named by a --restart=&lt;pid&gt; argument to exit, and
    /// reports whether this launch is such a restart.</summary>
    private static bool WaitForRestartPredecessor(string[] args)
    {
        const string prefix = "--restart=";
        var restartArg = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (restartArg is null || !int.TryParse(restartArg.AsSpan(prefix.Length), out var predecessorPid))
            return false;

        try
        {
            using var predecessor = Process.GetProcessById(predecessorPid);
            predecessor.WaitForExit(10000);
        }
        catch (ArgumentException)
        {
            // The old instance already exited before we looked - nothing to wait for.
        }

        return true;
    }

    private bool TryAcquireSingleInstance(bool isRestart)
    {
        // A normal launch tries exactly once, so a genuine second launch is detected instantly.
        // A restart retries briefly: WaitForExit returns the moment the predecessor is signaled
        // terminated, but the kernel can take a fraction longer to release its handle to the
        // named mutex - during which we'd still see it as held and wrongly bow out as a duplicate.
        var deadline = DateTime.UtcNow + (isRestart ? TimeSpan.FromSeconds(5) : TimeSpan.Zero);

        while (true)
        {
            var mutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
            if (createdNew)
            {
                _singleInstanceMutex = mutex;
                return true;
            }

            mutex.Dispose();
            if (DateTime.UtcNow >= deadline)
                return false;

            Thread.Sleep(100);
        }
    }
}
