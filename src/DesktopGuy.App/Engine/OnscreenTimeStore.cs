using System;
using System.Globalization;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers how many seconds a character has cumulatively spent onscreen,
/// per character folder, across restarts - just enough state to unlock a
/// special phrase after enough time spent together (see
/// CharacterController's companionship-time tracking). Best-effort only,
/// same as PositionStore/ScalePreferenceStore: any failure to read or write
/// just starts back at zero, never a crash.
/// </summary>
public static class OnscreenTimeStore
{
    public static double Load(string characterFolderName)
    {
        try
        {
            var text = File.ReadAllText(GetPath(characterFolderName)).Trim();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void Save(string characterFolderName, double totalSeconds)
    {
        try
        {
            File.WriteAllText(GetPath(characterFolderName), totalSeconds.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Not being able to remember the total isn't worth surfacing to the user.
        }
    }

    private static string GetPath(string characterFolderName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{characterFolderName}.onscreentime.txt");
    }
}
