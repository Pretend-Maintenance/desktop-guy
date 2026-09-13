using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Small Win32 helper so the overlay window behaves like a desktop
/// mascot rather than a normal app: no Alt+Tab entry, no taskbar button.
/// </summary>
internal static class Win32Interop
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    // Window classes that can legitimately cover the entire monitor
    // without being a "fullscreen app" in the sense this is checking for -
    // the desktop itself and the taskbar, mainly.
    private static readonly string[] NonFullscreenClassNames =
        { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    /// <summary>Releases an icon handle created via Bitmap.GetHicon() - that call transfers ownership to the caller, so it must be explicitly destroyed to avoid leaking a GDI handle.</summary>
    public static void DestroyIconHandle(IntPtr hIcon) => DestroyIcon(hIcon);

    /// <summary>
    /// Marks the window as a "tool window" (WS_EX_TOOLWINDOW) so it never
    /// shows up in Alt+Tab or the taskbar - just the little companion, no
    /// app chrome.
    /// </summary>
    public static void HideFromAltTabAndTaskbar(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    /// <summary>The process ID owning whichever window currently has focus, or null if that can't be determined.</summary>
    public static int? GetForegroundProcessId()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out uint processId);
        return processId == 0 ? null : (int)processId;
    }

    /// <summary>The title bar text of whichever window currently has focus, or "" if that can't be determined.</summary>
    public static string GetForegroundWindowTitle()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return "";
        }

        var buffer = new StringBuilder(256);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>
    /// True if the currently focused window covers the entire monitor it's
    /// on - the same rough heuristic taskbar-autohide-style tools use for
    /// "is a game/video/presentation running exclusively fullscreen right
    /// now". Excludes the desktop and taskbar's own window classes (which
    /// also happen to be monitor-sized) and this app's own window, so it
    /// doesn't hide itself just for being focused.
    /// </summary>
    public static bool IsForegroundWindowFullscreen(IntPtr ownHwnd)
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == ownHwnd)
        {
            return false;
        }

        var classNameBuffer = new StringBuilder(256);
        GetClassName(hwnd, classNameBuffer, classNameBuffer.Capacity);
        string className = classNameBuffer.ToString();
        if (Array.IndexOf(NonFullscreenClassNames, className) >= 0)
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return windowRect.Left <= info.rcMonitor.Left &&
               windowRect.Top <= info.rcMonitor.Top &&
               windowRect.Right >= info.rcMonitor.Right &&
               windowRect.Bottom >= info.rcMonitor.Bottom;
    }
}
