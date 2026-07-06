using System.Runtime.InteropServices;
using System.Text;

namespace TinyTimers.Services;

/// <summary>Resolves a running process's full executable path via QueryFullProcessImageName.
/// Process.MainModule needs PROCESS_VM_READ to walk the target's module list, which games and
/// anti-cheat-protected processes deny - they show a window but MainModule throws access-denied
/// or comes back empty (e.g. Rocket League). QueryFullProcessImageName only needs the lighter
/// PROCESS_QUERY_LIMITED_INFORMATION, which those processes still grant, so it resolves paths
/// MainModule can't.</summary>
internal static class ProcessImagePath
{
    private const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr handle, int flags, StringBuilder buffer, ref int size);

    /// <summary>The full path to the process's exe, or null if it couldn't be opened/queried
    /// (e.g. a system-protected process, or one that exited between enumeration and this call).</summary>
    public static string? TryGet(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
