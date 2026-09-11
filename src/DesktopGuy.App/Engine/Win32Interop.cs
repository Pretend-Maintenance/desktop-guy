using System;
using System.Runtime.InteropServices;

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
}
