using System;
using System.IO;
using System.Text.Json;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Remembers where you last left the character (per character id) between
/// runs, so it doesn't jump back to the same starting corner every time you
/// relaunch. Best-effort only - any failure to read or write just means the
/// character starts at its default spot, never a crash.
/// </summary>
public static class PositionStore
{
    private sealed class PositionData
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public static (double X, double Y)? TryLoad(string characterId)
    {
        try
        {
            var path = GetPath(characterId);
            if (!File.Exists(path))
            {
                return null;
            }

            var data = JsonSerializer.Deserialize<PositionData>(File.ReadAllText(path));
            return data is null ? null : (data.X, data.Y);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string characterId, double x, double y)
    {
        try
        {
            File.WriteAllText(GetPath(characterId), JsonSerializer.Serialize(new PositionData { X = x, Y = y }));
        }
        catch
        {
            // Not being able to remember position isn't worth surfacing to the user.
        }
    }

    private static string GetPath(string characterId)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopGuy");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{characterId}.position.json");
    }
}
