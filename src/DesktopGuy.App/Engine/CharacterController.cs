using System;
using System.Collections.Generic;
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
///   2. A terminal being focused, or you actively typing anywhere (hacking)
///      - checked before media, since "I'm clearly at the keyboard doing
///      something" is a stronger signal than background music. Both reuse
///      the same "hacking" animation - there's no separate art for
///      "typing in a random app" vs. "typing in a terminal".
///   3. Media context (music -> dance, video -> watch) - while active this
///      also suppresses the idle/sleep timer, since playing something is a
///      perfectly good reason not to be "away".
///   4. The regular idle/sleep/wander/speech behavior.
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
    private readonly Random _random = new();

    private double _minX;
    private double _maxX;
    private double _groundY;

    private double _walkTargetX;
    private double _walkVelocityX;
    private double _walkRemainingSeconds;
    private double _secondsUntilNextWalk;
    private double _secondsUntilNextSpeech;
    private bool _isFalling;
    private DiscordEvent? _pendingReaction;
    private WeatherCondition? _previewWeatherCondition;
    private double _previewWeatherSecondsRemaining;
    private bool _wasBatteryLow;

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
        BatteryWatcher? batteryContext = null)
    {
        _definition = definition;
        _mediaContext = mediaContext;
        _terminalContext = terminalContext;
        _typingContext = typingContext;
        _batteryContext = batteryContext;
        PositionX = startX;
        PositionY = startY;
        _secondsUntilNextWalk = RandomBetween(
            definition.Behavior.WalkIntervalMinSeconds, definition.Behavior.WalkIntervalMaxSeconds);
        _secondsUntilNextSpeech = RandomBetween(
            definition.Behavior.SpeechIntervalMinSeconds, definition.Behavior.SpeechIntervalMaxSeconds);
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

    /// <summary>Called by MainWindow once a one-shot reaction animation (answerCall/openMail) finishes.</summary>
    public void OnReactionAnimationFinished()
    {
        if (State == CharacterState.AnsweringCall || State == CharacterState.ReadingMessage)
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }
    }

    public void Tick(TimeSpan elapsed)
    {
        double dt = elapsed.TotalSeconds;

        TickBattery();

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

        if (State == CharacterState.AnsweringCall || State == CharacterState.ReadingMessage)
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

    /// <summary>
    /// Fires a one-off speech bubble the moment the battery drops to/below
    /// BatteryWatcher's low-battery threshold (edge-triggered on the
    /// transition, not every tick while it stays low, so it doesn't nag).
    /// No dedicated art for this - it's just a phrase, layered on top of
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
            return true;
        }

        if (context == MediaPlaybackContext.Music && HasAnimation(CharacterState.Dancing))
        {
            if (State != CharacterState.Dancing)
            {
                TransitionTo(CharacterState.Dancing);
            }
            return true;
        }

        // Media stopped while we were still showing a media state - fall
        // back to idle so the rest of Tick can take over normally.
        if (State == CharacterState.Dancing || State == CharacterState.Watching)
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }

        return false;
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
        int hour = DateTime.Now.Hour;
        List<string> timeSpecific = hour switch
        {
            >= 5 and < 12 => _definition.MorningPhrases,
            >= 18 and < 23 => _definition.EveningPhrases,
            >= 23 or < 5 => _definition.LateNightPhrases,
            _ => EmptyPhrases,
        };

        if (timeSpecific.Count == 0)
        {
            return _definition.Phrases;
        }

        var combined = new List<string>(_definition.Phrases.Count + timeSpecific.Count);
        combined.AddRange(_definition.Phrases);
        combined.AddRange(timeSpecific);
        return combined;
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
