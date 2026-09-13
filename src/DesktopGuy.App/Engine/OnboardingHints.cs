using System;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Tracks one-time onboarding nudges (e.g. "you could grant notification
/// access") shown across restarts and character switches - each hint has
/// its own marker file under %AppData%\DesktopGuy so it's shown at most
/// once ever, regardless of which character happens to be running when it
/// fires. Best-effort: if the marker can't be read or written, the hint
/// just risks showing again next time rather than crashing anything.
/// </summary>
public static class OnboardingHints
{
    public static bool ShouldShow(string hintId)
    {
        try
        {
            return !File.Exists(GetPath(hintId));
        }
        catch
        {
            return false;
        }
    }

    public static void MarkShown(string hintId)
    {
        try
        {
            File.WriteAllText(GetPath(hintId), "");
        }
        catch
        {
            // Not worth surfacing to the user - worst case the hint shows again.
        }
    }

    private static string GetPath(string hintId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"hint-{hintId}.shown");
    }
}
