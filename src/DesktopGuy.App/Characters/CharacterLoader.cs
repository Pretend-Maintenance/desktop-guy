using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopGuy.App.Characters;

/// <summary>
/// Discovers and loads character templates from Assets/Characters/*.
/// Each subfolder is one character: a character.json plus its sprite sheet.
/// </summary>
public static class CharacterLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string CharactersRootFolder =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");

    /// <summary>Lists the ids of every character folder that has a valid character.json.</summary>
    public static IReadOnlyList<string> DiscoverCharacterIds()
    {
        if (!Directory.Exists(CharactersRootFolder))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(CharactersRootFolder)
            .Where(dir => File.Exists(Path.Combine(dir, "character.json")))
            .Select(dir => Path.GetFileName(dir)!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Loads a character by folder name (e.g. "Blob"). Throws if the folder
    /// or its character.json / sprite sheet is missing so misconfigured
    /// templates fail fast instead of showing a blank window.
    /// </summary>
    public static CharacterDefinition Load(string characterFolderName)
    {
        var folder = Path.Combine(CharactersRootFolder, characterFolderName);
        var jsonPath = Path.Combine(folder, "character.json");

        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException(
                $"No character.json found for '{characterFolderName}' at {jsonPath}");
        }

        var json = File.ReadAllText(jsonPath);
        var definition = JsonSerializer.Deserialize<CharacterDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException($"Could not parse {jsonPath}");

        definition.SourceFolder = folder;

        var spriteSheetPath = Path.Combine(folder, definition.SpriteSheet);
        if (!File.Exists(spriteSheetPath))
        {
            throw new FileNotFoundException(
                $"Sprite sheet '{definition.SpriteSheet}' not found for character '{characterFolderName}' at {spriteSheetPath}");
        }

        return definition;
    }

    /// <summary>Loads the first available character, for a zero-config first run.</summary>
    public static CharacterDefinition LoadDefault()
    {
        var ids = DiscoverCharacterIds();
        if (ids.Count == 0)
        {
            throw new InvalidOperationException(
                $"No characters found under {CharactersRootFolder}. " +
                "Add a folder with a character.json and sprite sheet.");
        }

        return Load(ids[0]);
    }
}
