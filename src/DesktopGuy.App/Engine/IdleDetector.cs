using System;
using System.Runtime.InteropServices;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Reports how long it has been since the user last touched the mouse or
/// keyboard anywhere on the system (not just in our window), via the
/// Win32 GetLastInputInfo API. This is what lets the character notice
/// you've stepped away from the desktop and go to sleep.
/// </summary>
public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>();

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        uint idleTicks = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleTicks);
    }
}
