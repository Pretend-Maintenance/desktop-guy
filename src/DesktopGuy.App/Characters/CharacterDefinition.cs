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

    /// <summary>Shown at any time of day.</summary>
    public List<string> Phrases { get; set; } = new();

    /// <summary>Extra phrases mixed in alongside Phrases during roughly 5am-noon. Optional - leave empty for no morning-specific flavor.</summary>
    public List<string> MorningPhrases { get; set; } = new();

    /// <summary>Extra phrases mixed in alongside Phrases during roughly noon-6pm. Optional.</summary>
    public List<string> AfternoonPhrases { get; set; } = new();

    /// <summary>Extra phrases mixed in alongside Phrases during roughly 6pm-11pm. Optional.</summary>
    public List<string> EveningPhrases { get; set; } = new();

    /// <summary>Extra phrases mixed in alongside Phrases during roughly 11pm-5am. Optional.</summary>
    public List<string> LateNightPhrases { get; set; } = new();

    /// <summary>
    /// Extra phrases mixed in alongside Phrases only during a few fixed
    /// holiday windows (see CharacterController.GetSeasonalKey) - keyed by
    /// "halloween", "christmas", "newYear", "valentinesDay", "aprilFools",
    /// "bonfireNight", or "easter". Optional; leave a key out (or the whole
    /// thing empty) for no holiday-specific flavor.
    /// </summary>
    public Dictionary<string, List<string>> SeasonalPhrases { get; set; } = new();

    /// <summary>
    /// Extra phrases mixed in alongside MorningPhrases (so only during
    /// roughly 5am-noon, not all day) on the matching day of the week -
    /// keyed by "monday" through "sunday", lowercase. A chance for a
    /// character to greet the specific day in its own voice ("Happy
    /// Froodooo, it's the end of the week!") rather than just morning in
    /// general. Optional; leave a key out (or the whole thing empty) for
    /// no day-specific flavor.
    /// </summary>
    public Dictionary<string, List<string>> DayPhrases { get; set; } = new();

    /// <summary>
    /// Special lines that can be spoken instead of a normal phrase when a
    /// cumulative-onscreen-time milestone is reached (see
    /// CharacterController.TickOnscreenTime). Optional - a character with
    /// none just gets a generic fallback line.
    /// </summary>
    public List<string> AffectionPhrases { get; set; } = new();

    /// <summary>
    /// Milestone lines that call out the actual elapsed onscreen time,
    /// mixed into the same pool as AffectionPhrases at each milestone
    /// (see CharacterController.PickMilestonePhrase) rather than replacing
    /// it - so a milestone sometimes surfaces the real duration and
    /// sometimes just a general celebratory line. Use "{0}" as a
    /// placeholder - filled in with a friendly label like "an hour" or
    /// "a day". Optional - a character with none just never gets this
    /// specific flavor, falling back to a generic template instead.
    /// </summary>
    public List<string> TogetherTimePhrases { get; set; } = new();

    /// <summary>
    /// System-event phrase pools - all optional, and all fall back to a
    /// generic line (see the matching "Generic...Phrases" fields in
    /// CharacterController) when a character doesn't define its own. Each
    /// "Down"/"Low"/"Locked" style pool may use "{0}" as a placeholder -
    /// filled in with "wifi"/"internet" for network phrases, or the drive
    /// letter for USB phrases. Lock/unlock and disk-space phrases don't use
    /// a placeholder.
    /// </summary>
    public List<string> NetworkDownPhrases { get; set; } = new();
    public List<string> NetworkUpPhrases { get; set; } = new();
    public List<string> DiskSpaceLowPhrases { get; set; } = new();
    public List<string> DiskSpaceRecoveredPhrases { get; set; } = new();
    public List<string> SessionLockedPhrases { get; set; } = new();
    public List<string> SessionUnlockedPhrases { get; set; } = new();
    public List<string> UsbConnectedPhrases { get; set; } = new();
    public List<string> UsbDisconnectedPhrases { get; set; } = new();
    public List<string> BatteryPluggedInPhrases { get; set; } = new();

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

    /// <summary>
    /// How often, while sitting idle, he might spontaneously play a
    /// one-off "eating" or "playing" animation instead of just standing
    /// there - a random interval is picked between these two bounds after
    /// each one (and at startup). Only ever fires if the character
    /// actually defines an "eating" and/or "playing" animation.
    /// </summary>
    public double IdleSurpriseIntervalMinSeconds { get; set; } = 90;
    public double IdleSurpriseIntervalMaxSeconds { get; set; } = 240;
}
