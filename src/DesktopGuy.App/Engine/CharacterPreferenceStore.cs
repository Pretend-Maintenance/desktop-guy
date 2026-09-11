using System;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers which character folder was last selected (via the context
/// menu's character switcher), so a plain relaunch - or a "Start with
/// Windows" boot that wasn't pinned to a specific one - picks up where you
/// left off instead of always falling back to the first character folder
/// alphabetically. Best-effort only, same as PositionStore: any failure to
/// read or write just falls back to the default character, never a crash.
/// </summary>
public static class CharacterPreferenceStore
{
    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "selected-character.txt");
        }
    }

    public static string? TryLoad()
    {
        try
        {
            var text = File.ReadAllText(FilePath).Trim();
            return text.Length > 0 ? text : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string characterFolderName)
    {
        try
        {
            File.WriteAllText(FilePath, characterFolderName);
        }
        catch
        {
            // Not being able to remember the choice isn't worth surfacing to the user.
        }
    }
}
