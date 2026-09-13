using System;
using System.Collections.Generic;
using System.IO;
using DesktopGuy.App.Characters;
using DesktopGuy.App.Context;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Drives the little guy's behavior: noticing you've gone idle and
/// snoozing, occasionally wandering along the desktop, popping up random
/// remarks, reacting to being picked up, and reacting to what's going on
/// on the PC (music/video playing, a Discord call or message). Knows
/// nothing about WPF - MainWindow listens to its events and moves/animates
/// the actual window.
///
/// Priority each tick, highest first:
///   1. A pending reaction (Discord call/message) - interrupts anything
///      except an active drag, plays once, then falls through to whatever
///      is appropriate next tick.
///   2. A terminal or code editor being focused, or you actively typing
///      anywhere (hacking) - checked before media, since "I'm clearly at
///      the keyboard doing something" is a stronger signal than
///      background music. All of these reuse the same "hacking"
///      animation - there's no separate art for "typing in a random app"
///      vs. "typing in a terminal" vs. "in an IDE".
///   3. A video-call app being focused (Zoom, Teams, Google Meet, ...) -
///      reuses the "watching" animation, same reasoning as above.
///   4. Media context (music -> dance, video -> watch) - while active this
///      also suppresses the idle/sleep timer, since playing something is a
///      perfectly good reason not to be "away".
///   5. The regular idle/sleep/wander/speech behavior - which is also
///      where occasional idle-surprise animations (eating, playing, and
///      a low-battery mood if the character defines one) fit in: a random
///      one-off flourish while otherwise just standing around, the same
///      "plays once then falls through" shape as a Discord reaction.
///
/// Weather poses (cold/hot/sunny/rainy) exist as animations but aren't
/// triggered automatically - that used to depend on the real forecast
/// matching one of the four AND him being idle for a while at the same
/// time, which in practice meant they'd rarely show up. They're only ever
/// shown via PreviewWeather now, e.g. from the context menu's "Preview
/// Weather" submenu.
/// </summary>
public sealed class CharacterController
{
    private readonly CharacterDefinition _definition;
    private readonly MediaContextWatcher? _mediaContext;
    private readonly TerminalWatcher? _terminalContext;
    private readonly TypingWatcher? _typingContext;
    private readonly BatteryWatcher? _batteryContext;
    private readonly MeetingWatcher? _meetingContext;
    private readonly ScreenshotWatcher? _screenshotContext;
    private readonly Random _random = new();

    private double _minX;
    private double _maxX;
    private double _groundY;

    private double _walkTargetX;
    private double _walkVelocityX;
    private double _walkRemainingSeconds;
    private double _secondsUntilNextWalk;
    private double _secondsUntilNextSpeech;
    private double _secondsUntilNextIdleSurprise;
    private bool _isFalling;
    private DiscordEvent? _pendingReaction;
    private bool _pendingSnapshot;
    private WeatherCondition? _previewWeatherCondition;
    private double _previewWeatherSecondsRemaining;
    private bool _wasBatteryLow;
    private bool _wasBatteryFull;
    private double _totalSecondsOnscreen;
    private double _secondsSinceLastOnscreenSave;
    private string? _lastAnnouncedTrack;

    // Cumulative seconds spent onscreen (across restarts) at which a
    // special affection line plays once, in ascending order: 5 min, 30 min,
    // 1 hour, 4 hours, 1 day, 1 week, 30 days.
    private static readonly double[] OnscreenMilestoneSeconds =
        { 300, 1800, 3600, 14400, 86400, 604800, 2592000 };

    private const double OnscreenSaveIntervalSeconds = 60;

    private static readonly string[] GenericAffectionPhrases =
    {
        "We've been hanging out for a while now. I like that.",
        "Just noting: this is a good amount of time to spend together.",
        "Still here, still glad you're around.",
    };

    public CharacterState State { get; private set; } = CharacterState.Idle;
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public bool FacingRight { get; private set; } = false;

    public event Action<CharacterState>? StateChanged;
    public event Action? PositionChanged;
    public event Action<string>? SpeechRequested;

    public CharacterController(
        CharacterDefinition definition,
        double startX,
        double startY,
        MediaContextWatcher? mediaContext = null,
        TerminalWatcher? terminalContext = null,
        TypingWatcher? typingContext = null,
        BatteryWatcher? batteryContext = null,
        MeetingWatcher? meetingContext = null,
        ScreenshotWatcher? screenshotContext = null)
    {
        _definition = definition;
        _mediaContext = mediaContext;
        _terminalContext = terminalContext;
        _typingContext = typingContext;
        _batteryContext = batteryContext;
        _meetingContext = meetingContext;
        _screenshotContext = screenshotContext;
        PositionX = startX;
        PositionY = startY;
        _secondsUntilNextWalk = RandomBetween(
            definition.Behavior.WalkIntervalMinSeconds, definition.Behavior.WalkIntervalMaxSeconds);
        _secondsUntilNextSpeech = RandomBetween(
            definition.Behavior.SpeechIntervalMinSeconds, definition.Behavior.SpeechIntervalMaxSeconds);
        _secondsUntilNextIdleSurprise = RandomBetween(
            definition.Behavior.IdleSurpriseIntervalMinSeconds, definition.Behavior.IdleSurpriseIntervalMaxSeconds);
        _totalSecondsOnscreen = OnscreenTimeStore.Load(Path.GetFileName(definition.SourceFolder));
    }

    /// <summary>
    /// Total seconds this character has spent onscreen, including previous
    /// sessions - exposed so MainWindow can do one final save on shutdown
    /// (Tick itself only saves periodically, see TickOnscreenTime).
    /// </summary>
    public double TotalSecondsOnscreen => _totalSecondsOnscreen;

    /// <summary>Forces an immediate save of the onscreen-time total - call this when the app is closing.</summary>
    public void SaveOnscreenTime()
    {
        OnscreenTimeStore.Save(Path.GetFileName(_definition.SourceFolder), _totalSecondsOnscreen);
    }

    /// <summary>Confines wandering/dragging to the visible work area, and sets the "floor" the character rests on.</summary>
    public void SetBounds(double minX, double maxX, double groundY)
    {
        _minX = minX;
        _maxX = maxX;
        _groundY = groundY;
    }

    public void BeginDrag()
    {
        _isFalling = false;
        TransitionTo(CharacterState.Dragging);
    }

    /// <summary>Lets MainWindow tell the controller where the OS-level DragMove() actually left the window.</summary>
    public void SyncPosition(double x, double y)
    {
        PositionX = x;
        PositionY = y;
    }

    public void EndDrag()
    {
        if (PositionY < _groundY - 0.5)
        {
            _isFalling = true;
            TransitionTo(CharacterState.Idle);
        }
        else
        {
            PositionY = _groundY;
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }
    }

    /// <summary>
    /// Called by MainWindow when a click on the character turned out not to
    /// be a drag (see OnCharacterMouseDown) - there's no dedicated "petted"
    /// animation/art, so this just pops up a phrase immediately, as an
    /// acknowledgment that the click landed on him rather than doing
    /// nothing. Also pushes back the next ambient speech so they don't
    /// double up right after.
    /// </summary>
    public void OnPetted()
    {
        var pool = GetCurrentPhrasePool();
        if (pool.Count == 0)
        {
            return;
        }

        SpeechRequested?.Invoke(pool[_random.Next(pool.Count)]);
        _secondsUntilNextSpeech = _definition.Behavior.SpeechDurationSeconds + RandomBetween(
            _definition.Behavior.SpeechIntervalMinSeconds, _definition.Behavior.SpeechIntervalMaxSeconds);
    }

    /// <summary>
    /// Picks a milestone celebration line - the character's own
    /// AffectionPhrases if it defines any, otherwise a generic fallback -
    /// fired once at each threshold in OnscreenMilestoneSeconds.
    /// </summary>
    private string PickAffectionMilestonePhrase()
    {
        IReadOnlyList<string> pool = _definition.AffectionPhrases.Count > 0
            ? _definition.AffectionPhrases
            : GenericAffectionPhrases;

        return pool[_random.Next(pool.Count)];
    }

    /// <summary>
    /// Queues a one-shot reaction (phone up for a call, envelope for a
    /// message). Picked up on the next Tick unless the character is
    /// mid-drag, in which case it waits until the drag ends.
    /// </summary>
    public void RequestReaction(DiscordEvent discordEvent)
    {
        if (!HasAnimation(discordEvent == DiscordEvent.IncomingCall
                ? CharacterState.AnsweringCall
                : CharacterState.ReadingMessage))
        {
            return;
        }

        _pendingReaction = discordEvent;
    }

    /// <summary>
    /// Queues a one-shot startled reaction to the PrintScreen key being
    /// pressed. Same shape as RequestReaction above - picked up next Tick
    /// unless mid-drag.
    /// </summary>
    public void RequestSnapshotReaction()
    {
        if (!HasAnimation(CharacterState.Snapshot))
        {
            return;
        }

        _pendingSnapshot = true;
    }

    /// <summary>
    /// Forces a weather pose for a few seconds regardless of the actual
    /// weather or how long he's been idle - so it can be previewed from the
    /// context menu on demand, rather than waiting for it to genuinely be
    /// sunny/rainy/hot/cold outside and staying idle long enough to see it.
    /// </summary>
    public void PreviewWeather(WeatherCondition condition)
    {
        _previewWeatherCondition = condition;
        _previewWeatherSecondsRemaining = 5;
    }

    /// <summary>Called by MainWindow once the (non-looping) wake animation finishes playing.</summary>
    public void OnWakeAnimationFinished()
    {
        if (State == CharacterState.Waking)
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }
    }

    /// <summary>
    /// Called by MainWindow once a one-shot reaction animation finishes
    /// (answerCall/openMail/snapshot, or an idle surprise - eating/
    /// playing/lowBattery).
    /// </summary>
    public void OnReactionAnimationFinished()
    {
        if (IsOneShotReactionState(State))
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
            ScheduleNextIdleSurprise();
        }
    }

    private static bool IsOneShotReactionState(CharacterState state) => state is
        CharacterState.AnsweringCall or CharacterState.ReadingMessage or
        CharacterState.Snapshot or CharacterState.Eating or
        CharacterState.Playing or CharacterState.LowBattery;

    public void Tick(TimeSpan elapsed)
    {
        double dt = elapsed.TotalSeconds;

        TickBattery();
        TickOnscreenTime(dt);

        if (State == CharacterState.Dragging)
        {
            return;
        }

        if (TickWeatherPreview(dt))
        {
            return;
        }

        if (TryStartPendingReaction())
        {
            return;
        }

        if (TryStartPendingSnapshot())
        {
            return;
        }

        if (IsOneShotReactionState(State))
        {
            // Waiting on OnReactionAnimationFinished to move on.
            return;
        }

        if (_isFalling)
        {
            TickFalling(dt);
            return;
        }

        if (TickTerminalContext())
        {
            return;
        }

        if (TickMeetingContext())
        {
            return;
        }

        if (TickMediaContext())
        {
            return;
        }

        double idleSeconds = IdleDetector.GetIdleTime().TotalSeconds;
        bool userIsAway = idleSeconds >= _definition.Behavior.IdleTimeoutSeconds;

        if (userIsAway)
        {
            if (State != CharacterState.Sleeping && State != CharacterState.Waking)
            {
                TransitionTo(CharacterState.Sleeping);
            }
            return;
        }

        if (State == CharacterState.Sleeping)
        {
            TransitionTo(CharacterState.Waking);
            return;
        }

        if (State == CharacterState.Waking)
        {
            // Waiting on OnWakeAnimationFinished to move on.
            return;
        }

        if (TickIdleSurprise(dt))
        {
            return;
        }

        TickWalking(dt);
        TickSpeech(dt);
    }

    private bool TryStartPendingReaction()
    {
        if (_pendingReaction is null)
        {
            return false;
        }

        var reaction = _pendingReaction.Value;
        _pendingReaction = null;
        _isFalling = false;

        TransitionTo(reaction == DiscordEvent.IncomingCall
            ? CharacterState.AnsweringCall
            : CharacterState.ReadingMessage);
        return true;
    }

    private bool TryStartPendingSnapshot()
    {
        if (!_pendingSnapshot)
        {
            return false;
        }

        _pendingSnapshot = false;
        _isFalling = false;

        TransitionTo(CharacterState.Snapshot);
        return true;
    }

    /// <summary>
    /// Occasionally plays a spontaneous "eating" or "playing" animation -
    /// or "lowBattery" too, while the battery's actually low - instead of
    /// just standing there idle. Only ever fires while genuinely idle
    /// (not mid-walk), and only considers animations the character
    /// actually defines, so it's a complete no-op for any character
    /// (or state) that doesn't have this art.
    /// </summary>
    private bool TickIdleSurprise(double dt)
    {
        if (State != CharacterState.Idle)
        {
            return false;
        }

        var candidates = new List<CharacterState>(3);
        if (HasAnimation(CharacterState.Eating)) candidates.Add(CharacterState.Eating);
        if (HasAnimation(CharacterState.Playing)) candidates.Add(CharacterState.Playing);
        if ((_batteryContext?.IsLow ?? false) && HasAnimation(CharacterState.LowBattery))
        {
            candidates.Add(CharacterState.LowBattery);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        _secondsUntilNextIdleSurprise -= dt;
        if (_secondsUntilNextIdleSurprise > 0)
        {
            return false;
        }

        TransitionTo(candidates[_random.Next(candidates.Count)]);
        return true;
    }

    private void ScheduleNextIdleSurprise()
    {
        _secondsUntilNextIdleSurprise = RandomBetween(
            _definition.Behavior.IdleSurpriseIntervalMinSeconds, _definition.Behavior.IdleSurpriseIntervalMaxSeconds);
    }

    /// <summary>
    /// Fires a one-off speech bubble the moment the battery drops to/below
    /// BatteryWatcher's low-battery threshold, or the moment it reaches a
    /// full charge while plugged in - both edge-triggered on the
    /// transition, not every tick while they hold, so it doesn't nag.
    /// No dedicated art for either - just a phrase, layered on top of
    /// whatever else is going on rather than changing state/animation.
    /// </summary>
    private void TickBattery()
    {
        bool isLow = _batteryContext?.IsLow ?? false;
        if (isLow && !_wasBatteryLow)
        {
            SpeechRequested?.Invoke("Battery's getting low - might want to plug in!");
        }

        _wasBatteryLow = isLow;

        bool isFull = _batteryContext?.IsFull ?? false;
        if (isFull && !_wasBatteryFull)
        {
            SpeechRequested?.Invoke("Battery's fully charged!");
        }

        _wasBatteryFull = isFull;
    }

    /// <summary>
    /// Accumulates total onscreen time (regardless of what state the
    /// character is in - sleeping, dragging, dancing, all of it counts as
    /// "here"), fires a one-off affection line the moment cumulative time
    /// crosses a threshold in OnscreenMilestoneSeconds, and periodically
    /// persists the running total so it survives a restart without writing
    /// to disk every single frame.
    /// </summary>
    private void TickOnscreenTime(double dt)
    {
        double previousTotal = _totalSecondsOnscreen;
        _totalSecondsOnscreen += dt;

        foreach (var milestone in OnscreenMilestoneSeconds)
        {
            if (previousTotal < milestone && _totalSecondsOnscreen >= milestone)
            {
                SpeechRequested?.Invoke(PickAffectionMilestonePhrase());
                _secondsUntilNextSpeech = _definition.Behavior.SpeechDurationSeconds + RandomBetween(
                    _definition.Behavior.SpeechIntervalMinSeconds, _definition.Behavior.SpeechIntervalMaxSeconds);
                break;
            }
        }

        _secondsSinceLastOnscreenSave += dt;
        if (_secondsSinceLastOnscreenSave >= OnscreenSaveIntervalSeconds)
        {
            _secondsSinceLastOnscreenSave = 0;
            SaveOnscreenTime();
        }
    }

    /// <summary>Returns true if a focused terminal or active typing took over this tick.</summary>
    private bool TickTerminalContext()
    {
        bool terminalFocused = _terminalContext?.IsActive ?? false;
        bool typing = _typingContext?.IsTyping ?? false;

        if ((terminalFocused || typing) && HasAnimation(CharacterState.Hacking))
        {
            if (State != CharacterState.Hacking)
            {
                TransitionTo(CharacterState.Hacking);
            }
            return true;
        }

        if (State == CharacterState.Hacking)
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }

        return false;
    }

    /// <summary>
    /// Returns true if a focused video-call app (Zoom, Teams, Google Meet,
    /// ...) took over this tick. No dedicated cleanup-to-Idle branch here
    /// on purpose - TickMediaContext's own fallback (a few lines down)
    /// already resets out of Watching whenever neither it nor this one
    /// still wants it, since that check doesn't care which watcher set
    /// the state in the first place.
    /// </summary>
    private bool TickMeetingContext()
    {
        bool inMeeting = _meetingContext?.IsActive ?? false;

        if (inMeeting && HasAnimation(CharacterState.Watching))
        {
            if (State != CharacterState.Watching)
            {
                TransitionTo(CharacterState.Watching);
            }
            return true;
        }

        return false;
    }

    /// <summary>Returns true if a media context (music/video) took over this tick.</summary>
    private bool TickMediaContext()
    {
        var context = _mediaContext?.Current ?? MediaPlaybackContext.None;

        if (context == MediaPlaybackContext.Video && HasAnimation(CharacterState.Watching))
        {
            if (State != CharacterState.Watching)
            {
                TransitionTo(CharacterState.Watching);
            }
            AnnounceNowPlayingIfChanged();
            return true;
        }

        if (context == MediaPlaybackContext.Music && HasAnimation(CharacterState.Dancing))
        {
            if (State != CharacterState.Dancing)
            {
                TransitionTo(CharacterState.Dancing);
            }
            AnnounceNowPlayingIfChanged();
            return true;
        }

        // Media stopped while we were still showing a media state - fall
        // back to idle so the rest of Tick can take over normally, and
        // forget the last-announced track so the same song coming back
        // later (e.g. a loop) gets announced again.
        if (State == CharacterState.Dancing || State == CharacterState.Watching)
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }

        _lastAnnouncedTrack = null;
        return false;
    }

    /// <summary>
    /// Pops up a one-off "now playing" bubble the moment a new track/video
    /// title is seen - edge-triggered on the title actually changing, not
    /// every tick, so it doesn't repeat itself for as long as the same
    /// thing keeps playing. Silently does nothing if the app playing it
    /// didn't report a title (common for some browser tabs).
    /// </summary>
    private void AnnounceNowPlayingIfChanged()
    {
        string? title = _mediaContext?.CurrentTitle;
        if (title is null || title == _lastAnnouncedTrack)
        {
            return;
        }

        _lastAnnouncedTrack = title;

        string? artist = _mediaContext?.CurrentArtist;
        string announcement = string.IsNullOrEmpty(artist)
            ? $"~ Now playing: {title}"
            : $"~ Now playing: {artist} - {title}";

        SpeechRequested?.Invoke(announcement);
        _secondsUntilNextSpeech = _definition.Behavior.SpeechDurationSeconds + RandomBetween(
            _definition.Behavior.SpeechIntervalMinSeconds, _definition.Behavior.SpeechIntervalMaxSeconds);
    }

    /// <summary>Returns true if a manually-triggered weather preview (see PreviewWeather) took over this tick.</summary>
    private bool TickWeatherPreview(double dt)
    {
        if (_previewWeatherCondition is not { } condition)
        {
            return false;
        }

        CharacterState? state = condition switch
        {
            WeatherCondition.Rainy => CharacterState.WeatherRainy,
            WeatherCondition.Cold => CharacterState.WeatherCold,
            WeatherCondition.Hot => CharacterState.WeatherHot,
            WeatherCondition.Sunny => CharacterState.WeatherSunny,
            _ => null,
        };

        if (state is null || !HasAnimation(state.Value))
        {
            _previewWeatherCondition = null;
            return false;
        }

        if (State != state.Value)
        {
            TransitionTo(state.Value);
        }

        _previewWeatherSecondsRemaining -= dt;
        if (_previewWeatherSecondsRemaining <= 0)
        {
            _previewWeatherCondition = null;
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }

        return true;
    }

    private void TickFalling(double dt)
    {
        const double fallSpeedPxPerSec = 260;
        double remaining = _groundY - PositionY;

        if (remaining <= 1)
        {
            PositionY = _groundY;
            _isFalling = false;
            ScheduleNextWalk();
            PositionChanged?.Invoke();
            return;
        }

        PositionY += Math.Min(remaining, fallSpeedPxPerSec * dt);
        PositionChanged?.Invoke();
    }

    private void TickWalking(double dt)
    {
        if (State == CharacterState.Walking)
        {
            _walkRemainingSeconds -= dt;
            PositionX = Clamp(PositionX + _walkVelocityX * dt, _minX, _maxX);
            PositionChanged?.Invoke();

            bool reachedTarget = Math.Abs(PositionX - _walkTargetX) < 2;
            bool hitBounds = PositionX <= _minX || PositionX >= _maxX;

            if (_walkRemainingSeconds <= 0 || reachedTarget || hitBounds)
            {
                TransitionTo(CharacterState.Idle);
                ScheduleNextWalk();
            }

            return;
        }

        _secondsUntilNextWalk -= dt;
        if (_secondsUntilNextWalk <= 0)
        {
            StartWalking();
        }
    }

    private void StartWalking()
    {
        double duration = RandomBetween(
            _definition.Behavior.WalkDurationMinSeconds, _definition.Behavior.WalkDurationMaxSeconds);

        double maxTravel = _definition.Behavior.WalkSpeedPxPerSec * duration;
        double direction = _random.Next(2) == 0 ? -1 : 1;

        // Nudge back toward the middle of the bounds if we're pinned at an edge.
        if (PositionX <= _minX + 4) direction = 1;
        if (PositionX >= _maxX - 4) direction = -1;

        _walkTargetX = Clamp(PositionX + direction * maxTravel, _minX, _maxX);
        _walkVelocityX = (_walkTargetX - PositionX) / duration;
        _walkRemainingSeconds = duration;
        FacingRight = _walkVelocityX >= 0;

        TransitionTo(CharacterState.Walking);
    }

    private void ScheduleNextWalk()
    {
        _secondsUntilNextWalk = RandomBetween(
            _definition.Behavior.WalkIntervalMinSeconds, _definition.Behavior.WalkIntervalMaxSeconds);
    }

    private void TickSpeech(double dt)
    {
        var pool = GetCurrentPhrasePool();
        if (pool.Count == 0)
        {
            return;
        }

        _secondsUntilNextSpeech -= dt;
        if (_secondsUntilNextSpeech <= 0)
        {
            string phrase = pool[_random.Next(pool.Count)];
            SpeechRequested?.Invoke(phrase);

            _secondsUntilNextSpeech = _definition.Behavior.SpeechDurationSeconds + RandomBetween(
                _definition.Behavior.SpeechIntervalMinSeconds, _definition.Behavior.SpeechIntervalMaxSeconds);
        }
    }

    /// <summary>
    /// The general phrase pool, plus whichever time-of-day pool (morning/
    /// evening/late-night) matches the clock right now, if that character
    /// defines any for this time - so "he mentions coffee in the morning"
    /// is just extra lines layered on top of the always-available ones,
    /// not a separate thing that replaces them.
    /// </summary>
    private IReadOnlyList<string> GetCurrentPhrasePool()
    {
        var now = DateTime.Now;
        List<string> timeSpecific = now.Hour switch
        {
            >= 5 and < 12 => _definition.MorningPhrases,
            >= 18 and < 23 => _definition.EveningPhrases,
            >= 23 or < 5 => _definition.LateNightPhrases,
            _ => EmptyPhrases,
        };

        List<string> seasonal = GetSeasonalKey(now) is { } key && _definition.SeasonalPhrases.TryGetValue(key, out var list)
            ? list
            : EmptyPhrases;

        if (timeSpecific.Count == 0 && seasonal.Count == 0)
        {
            return _definition.Phrases;
        }

        var combined = new List<string>(_definition.Phrases.Count + timeSpecific.Count + seasonal.Count);
        combined.AddRange(_definition.Phrases);
        combined.AddRange(timeSpecific);
        combined.AddRange(seasonal);
        return combined;
    }

    /// <summary>
    /// A handful of fixed, deliberately narrow holiday windows - not meant
    /// to cover every possible date, just enough to make the character feel
    /// a little seasonally aware without any external calendar/timezone
    /// dependency. Returns null (no seasonal flavor) for every other day.
    /// </summary>
    private static string? GetSeasonalKey(DateTime now)
    {
        int month = now.Month;
        int day = now.Day;

        if (month == 10 && day >= 25)
        {
            return "halloween";
        }

        if (month == 12 && day >= 20 && day <= 26)
        {
            return "christmas";
        }

        if ((month == 12 && day == 31) || (month == 1 && day == 1))
        {
            return "newYear";
        }

        return null;
    }

    private static readonly List<string> EmptyPhrases = new();

    private bool HasAnimation(CharacterState state) =>
        _definition.Animations.ContainsKey(CharacterAnimationMap.GetAnimationName(state));

    private void TransitionTo(CharacterState newState)
    {
        if (State == newState)
        {
            return;
        }

        State = newState;
        StateChanged?.Invoke(newState);
    }

    private double RandomBetween(double min, double max) => min + _random.NextDouble() * (max - min);

    private static double Clamp(double value, double min, double max) =>
        min > max ? min : Math.Clamp(value, min, max);
}
