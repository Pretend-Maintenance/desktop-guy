using System;
using System.Globalization;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers a per-character display scale chosen from the context menu's
/// size submenu, so it survives the relaunch that applying a new scale
/// requires (see MainWindow.OnScaleSelected) and any plain restart after
/// that. Best-effort only, same as PositionStore/CharacterPreferenceStore:
/// any failure to read or write just falls back to the character's
/// authored default scale, never a crash.
/// </summary>
public static class ScalePreferenceStore
{
    public static double? TryLoad(string characterFolderName)
    {
        try
        {
            var text = File.ReadAllText(GetPath(characterFolderName)).Trim();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                ? scale
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string characterFolderName, double scale)
    {
        try
        {
            File.WriteAllText(GetPath(characterFolderName), scale.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Not being able to remember the choice isn't worth surfacing to the user.
        }
    }

    private static string GetPath(string characterFolderName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{characterFolderName}.scale.txt");
    }
}
