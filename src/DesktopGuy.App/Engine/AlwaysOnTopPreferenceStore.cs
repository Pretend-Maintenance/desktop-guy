using System;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers whether the character should stay topmost (always drawn over
/// other windows), toggled from the context menu's "Always on Top" item.
/// A single global preference, not per-character - defaults to true, since
/// that was the previous hardcoded behavior (Topmost="True" in XAML)
/// before this became toggleable.
/// </summary>
public static class AlwaysOnTopPreferenceStore
{
    public static bool Load()
    {
        try
        {
            var text = File.ReadAllText(GetPath()).Trim();
            return !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    public static void Save(bool enabled)
    {
        try
        {
            File.WriteAllText(GetPath(), enabled ? "true" : "false");
        }
        catch
        {
            // Not being able to remember the choice isn't worth surfacing to the user.
        }
    }

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "alwaysontop.txt");
    }
}
