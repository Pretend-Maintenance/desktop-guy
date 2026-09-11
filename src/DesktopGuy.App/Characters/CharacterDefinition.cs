using System.Collections.Generic;

namespace DesktopGuy.App.Characters;

/// <summary>
/// The full template for a companion character, loaded from a
/// character.json file that sits next to a sprite sheet PNG.
/// Add a new character by dropping a new folder under
/// Assets/Characters/&lt;Name&gt;/ with these two files - no code changes needed.
/// </summary>
public sealed class CharacterDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string SpriteSheet { get; set; } = "spritesheet.png";
    public FrameSize FrameSize { get; set; } = new();
    public double Scale { get; set; } = 4.0;

    /// <summary>
    /// Cross-fades between animation frames instead of hard-cutting, so a
    /// handful of sprite-sheet poses read as smoother motion. Good for
    /// smooth-shaded/cartoon art; usually looks wrong for blocky pixel art
    /// (defaults to false, the hard-cut look).
    /// </summary>
    public bool SmoothTransitions { get; set; } = false;

    public Dictionary<string, AnimationDefinition> Animations { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public List<string> Phrases { get; set; } = new();

    /// <summary>Absolute path to the folder this definition was loaded from.</summary>
    public string SourceFolder { get; set; } = "";
}

public sealed class FrameSize
{
    public int Width { get; set; } = 32;
    public int Height { get; set; } = 32;
}

/// <summary>
/// One row of the sprite sheet: which row index holds the frames, how many
/// frames it has, and how fast to play them.
/// </summary>
public sealed class AnimationDefinition
{
    public int Row { get; set; }
    public int FrameCount { get; set; } = 1;
    public double Fps { get; set; } = 4;
    public bool Loop { get; set; } = true;
}

public sealed class BehaviorSettings
{
    public double IdleTimeoutSeconds { get; set; } = 60;
    public double WalkSpeedPxPerSec { get; set; } = 40;
    public double WalkIntervalMinSeconds { get; set; } = 8;
    public double WalkIntervalMaxSeconds { get; set; } = 25;
    public double WalkDurationMinSeconds { get; set; } = 2;
    public double WalkDurationMaxSeconds { get; set; } = 6;
    public double SpeechIntervalMinSeconds { get; set; } = 30;
    public double SpeechIntervalMaxSeconds { get; set; } = 90;
    public double SpeechDurationSeconds { get; set; } = 4;
    public double ColdThresholdCelsius { get; set; } = 5;
    public double HotThresholdCelsius { get; set; } = 25;
}
