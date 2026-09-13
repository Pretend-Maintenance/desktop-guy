using System;
using System.Windows.Forms;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Picks which monitor's work area the character should wander/rest
/// within. Previously this was always `SystemParameters.WorkArea`, which
/// WPF documents as the *primary* monitor's work area only - meaning a
/// second monitor was permanently unreachable (wandering, drag physics,
/// and the ground line were all pinned to monitor 1), and a position
/// remembered on a second monitor would snap back onto monitor 1's edge
/// on every restart. This picks whichever monitor a remembered X position
/// actually falls on, or the primary monitor for a fresh install or if
/// that monitor's been disconnected since.
///
/// Caveat: System.Windows.Forms.Screen reports bounds in raw pixels,
/// used here directly as WPF's device-independent units. That's exact
/// when every monitor runs the same DPI scale (the common case,
/// especially on a desktop with matched monitors); on a mixed-DPI setup
/// (e.g. a laptop's own screen at 150% next to an external monitor at
/// 100%) the numbers can be slightly off on a non-primary monitor - still
/// strictly better than being unable to use anything but the primary
/// monitor at all.
/// </summary>
public static class MonitorLayout
{
    public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

    /// <summary>The work area of whichever monitor contains the given X, or the primary monitor if x is null or off every currently connected monitor.</summary>
    public static WorkArea GetWorkAreaFor(double? x)
    {
        try
        {
            if (x is { } knownX)
            {
                foreach (var screen in Screen.AllScreens)
                {
                    if (knownX >= screen.WorkingArea.Left && knownX < screen.WorkingArea.Right)
                    {
                        return ToWorkArea(screen.WorkingArea);
                    }
                }
            }

            if (Screen.PrimaryScreen is { } primary)
            {
                return ToWorkArea(primary.WorkingArea);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Record("MonitorLayout.GetWorkAreaFor", ex);
        }

        // Last-resort fallback if System.Windows.Forms.Screen is somehow
        // unavailable - WPF's own primary-monitor-only work area.
        var wpfArea = System.Windows.SystemParameters.WorkArea;
        return new WorkArea(wpfArea.Left, wpfArea.Top, wpfArea.Right, wpfArea.Bottom);
    }

    private static WorkArea ToWorkArea(System.Drawing.Rectangle rect) =>
        new(rect.Left, rect.Top, rect.Right, rect.Bottom);
}
