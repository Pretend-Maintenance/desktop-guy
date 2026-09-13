using System;
using System.Collections.Generic;
using System.IO;

namespace DesktopGuy.App.Engine;

/// <summary>
/// A tiny rolling log of the last 10 errors that any background piece of
/// the app (a context watcher failing to start, an unhandled exception)
/// ran into, written to %AppData%\DesktopGuy\errors.log. Every failure
/// recorded here is already non-fatal on its own - each watcher degrades
/// gracefully and the character keeps running regardless - this only
/// exists so "why isn't X detecting" is answerable by opening a text file
/// instead of needing a debugger attached. One line per entry, oldest
/// entries dropped once there are more than 10.
/// </summary>
public static class ErrorLog
{
    private const int MaxEntries = 10;
    private static readonly object Lock = new();

    public static void Record(string source, Exception ex)
    {
        try
        {
            lock (Lock)
            {
                var entries = ReadEntries();
                entries.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] {ex.GetType().Name}: {ex.Message}");
                if (entries.Count > MaxEntries)
                {
                    entries.RemoveRange(0, entries.Count - MaxEntries);
                }

                File.WriteAllLines(GetPath(), entries);
            }
        }
        catch
        {
            // Logging is itself best-effort - a failure here shouldn't cascade
            // into the thing it was trying to record a failure about.
        }
    }

    /// <summary>Opens the log file with whatever the system's default text viewer is, creating an empty one first if needed.</summary>
    public static string GetOrCreatePath()
    {
        string path = GetPath();
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "No errors recorded yet.\n");
        }

        return path;
    }

    private static List<string> ReadEntries()
    {
        try
        {
            return new List<string>(File.ReadAllLines(GetPath()));
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string GetPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "errors.log");
    }
}
