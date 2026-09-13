using System;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Wipes every bit of state the app keeps: remembered position/scale/
/// character choice, onscreen-time and pet-milestone progress, the error
/// log, and which one-time onboarding hints have already been shown -
/// everything under %AppData%\DesktopGuy - plus turns off "Start with
/// Windows". Used by the "Reset All Settings" menu item for a clean-slate
/// restart; not something any of the individual *Store classes need to
/// know about themselves, since they already tolerate their files simply
/// not existing.
/// </summary>
public static class AppDataReset
{
    public static void ResetAll()
    {
        try
        {
            StartupRegistration.SetEnabled(false, "");
        }
        catch
        {
            // Not worth blocking the rest of the reset over - worst case
            // the old startup entry is still there pointing at a character
            // that may no longer be the one you launch into.
        }

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
