using System;
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
///   4. The regular idle/sleep/wander/speech behavior, which is where
///      weather (cold/hot/sunny/rainy) fits in - it does NOT suppress
///      sleep (being cold outside all day shouldn't keep him up forever).
///      It also isn't instant: he has to be idle for a stretch first (see
///      BehaviorSettings.WeatherIdleDelaySeconds) before settling into the
///      weather pose, and even then the normal wander schedule can still
///      pull him out of it - it's "he'll chill in the sun for a while",
///      not "he's frozen there until the weather changes".
/// </summary>
public sealed class CharacterController
{
    private readonly CharacterDefinition _definition;
    private readonly MediaContextWatcher? _mediaContext;
    private readonly TerminalWatcher? _terminalContext;
    private readonly TypingWatcher? _typingContext;
    private readonly WeatherWatcher? _weatherContext;
    private readonly Random _random = new();

    private double _minX;
    private double _maxX;
    private double _groundY;

    private double _walkTargetX;
    private double _walkVelocityX;
    private double _walkRemainingSeconds;
    private double _secondsUntilNextWalk;
    private double _secondsUntilNextSpeech;
    private double _secondsIdleForWeather;
    private bool _isFalling;
    private DiscordEvent? _pendingReaction;

    public CharacterState State { get; private set; } = CharacterState.Idle;
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public bool FacingRight { get; private set; } = true;

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
        WeatherWatcher? weatherContext = null)
    {
        _definition = definition;
        _mediaContext = mediaContext;
        _terminalContext = terminalContext;
        _typingContext = typingContext;
        _weatherContext = weatherContext;
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

        if (State == CharacterState.Dragging)
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

        if (State == CharacterState.Idle)
        {
            _secondsIdleForWeather += dt;
        }
        else if (State == CharacterState.Walking)
        {
            _secondsIdleForWeather = 0;
        }

        if (TickWeatherContext(dt))
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

    /// <summary>Returns true if the current weather took over this tick.</summary>
    private bool TickWeatherContext(double dt)
    {
        CharacterState? weatherState = (_weatherContext?.Current ?? WeatherCondition.None) switch
        {
            WeatherCondition.Rainy => CharacterState.WeatherRainy,
            WeatherCondition.Cold => CharacterState.WeatherCold,
            WeatherCondition.Hot => CharacterState.WeatherHot,
            WeatherCondition.Sunny => CharacterState.WeatherSunny,
            _ => null,
        };

        if (weatherState is { } state && HasAnimation(state))
        {
            bool alreadyChilling = State == state;

            if (!alreadyChilling)
            {
                // Don't interrupt active wandering just because the weather
                // matches - only settle into the pose once he's actually
                // been standing around a while.
                if (_secondsIdleForWeather < _definition.Behavior.WeatherIdleDelaySeconds)
                {
                    return false;
                }

                TransitionTo(state);
                return true;
            }

            // Still let the normal wander schedule pull him out of it every
            // so often, rather than camping in the weather pose forever.
            _secondsUntilNextWalk -= dt;
            if (_secondsUntilNextWalk <= 0)
            {
                _secondsIdleForWeather = 0;
                TransitionTo(CharacterState.Idle);
                return false;
            }

            return true;
        }

        if (IsWeatherState(State))
        {
            TransitionTo(CharacterState.Idle);
            ScheduleNextWalk();
        }

        return false;
    }

    private static bool IsWeatherState(CharacterState state) => state is
        CharacterState.WeatherCold or CharacterState.WeatherHot or
        CharacterState.WeatherSunny or CharacterState.WeatherRainy;

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
        if (_definition.Phrases.Count == 0)
        {
            return;
        }

        _secondsUntilNextSpeech -= dt;
        if (_secondsUntilNextSpeech <= 0)
        {
            string phrase = _definition.Phrases[_random.Next(_definition.Phrases.Count)];
            SpeechRequested?.Invoke(phrase);

            _secondsUntilNextSpeech = _definition.Behavior.SpeechDurationSeconds + RandomBetween(
                _definition.Behavior.SpeechIntervalMinSeconds, _definition.Behavior.SpeechIntervalMaxSeconds);
        }
    }

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
