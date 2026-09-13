using System;
using System.Globalization;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers how many times a character has been petted, per character
/// folder, across restarts - just enough state to unlock a special phrase
/// at a few milestones (see CharacterController's affection tracking).
/// Best-effort only, same as PositionStore/ScalePreferenceStore: any
/// failure to read or write just starts back at zero, never a crash.
/// </summary>
public static class PetCountStore
{
    public static int Load(string characterFolderName)
    {
        try
        {
            var text = File.ReadAllText(GetPath(characterFolderName)).Trim();
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void Save(string characterFolderName, int count)
    {
        try
        {
            File.WriteAllText(GetPath(characterFolderName), count.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Not being able to remember the count isn't worth surfacing to the user.
        }
    }

    private static string GetPath(string characterFolderName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{characterFolderName}.petcount.txt");
    }
}
