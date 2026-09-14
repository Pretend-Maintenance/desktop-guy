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
/// System.Windows.Forms.Screen reports bounds in physical pixels, but
/// WPF's Window.Left/Top/Width/Height are in device-independent units
/// (96 per inch) - on anything other than exactly 100% display scaling
/// (125%/150% is the Windows default on most modern displays), using
/// those pixel values directly would place the window far outside the
/// actual visible area, which is exactly what happened the first time
/// this shipped. Every Screen rect is converted to WPF units via a scale
/// factor derived from comparing SystemParameters.WorkArea (already
/// correctly DPI-converted, but primary-monitor-only) against the
/// primary Screen's own raw-pixel work area - assumes every monitor runs
/// the same DPI scale, which holds for a single monitor (now handled
/// correctly again) and for the common multi-monitor case of matched
/// displays; a mixed-DPI multi-monitor setup (a laptop's own screen at
/// 150% next to an external at 100%) can still be slightly off on the
/// non-primary monitor.
/// </summary>
public static class MonitorLayout
{
    public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);

    private static readonly double ScaleFactor = ComputeScaleFactor();

    /// <summary>The work area of whichever monitor contains the given X, or the primary monitor if x is null or off every currently connected monitor.</summary>
    public static WorkArea GetWorkAreaFor(double? x)
    {
        try
        {
            foreach (var screen in Screen.AllScreens)
            {
                var area = ToWorkArea(screen.WorkingArea);
                if (x is { } knownX && knownX >= area.Left && knownX < area.Right)
                {
                    return area;
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
        // unavailable - WPF's own primary-monitor-only work area, already
        // in the right units with no conversion needed.
        var wpfArea = System.Windows.SystemParameters.WorkArea;
        return new WorkArea(wpfArea.Left, wpfArea.Top, wpfArea.Right, wpfArea.Bottom);
    }

    private static WorkArea ToWorkArea(System.Drawing.Rectangle rect) => new(
        rect.Left * ScaleFactor, rect.Top * ScaleFactor, rect.Right * ScaleFactor, rect.Bottom * ScaleFactor);

    /// <summary>
    /// WPF DIP units per physical pixel, e.g. ~0.667 at 150% scaling. Derived
    /// once from two already-available values rather than any DPI-specific
    /// Win32 API - SystemParameters.WorkArea.Width is the primary monitor's
    /// width in DIPs, Screen.PrimaryScreen.WorkingArea.Width is the same
    /// monitor's width in physical pixels; their ratio is exactly 96/actualDPI.
    /// </summary>
    private static double ComputeScaleFactor()
    {
        try
        {
            double dipWidth = System.Windows.SystemParameters.WorkArea.Width;
            double pixelWidth = Screen.PrimaryScreen?.WorkingArea.Width ?? 0;
            if (dipWidth > 0 && pixelWidth > 0)
            {
                return dipWidth / pixelWidth;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Record("MonitorLayout.ComputeScaleFactor", ex);
        }

        return 1.0;
    }
}
