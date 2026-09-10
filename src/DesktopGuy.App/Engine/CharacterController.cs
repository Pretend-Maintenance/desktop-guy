using System;
using DesktopGuy.App.Characters;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Drives the little guy's behavior: noticing you've gone idle and
/// snoozing, occasionally wandering along the desktop, popping up random
/// remarks, and reacting to being picked up. Knows nothing about WPF -
/// MainWindow listens to its events and moves/animates the actual window.
/// </summary>
public sealed class CharacterController
{
    private readonly CharacterDefinition _definition;
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

    public CharacterState State { get; private set; } = CharacterState.Idle;
    public double PositionX { get; private set; }
    public double PositionY { get; private set; }
    public bool FacingRight { get; private set; } = true;

    public event Action<CharacterState>? StateChanged;
    public event Action? PositionChanged;
    public event Action<string>? SpeechRequested;

    public CharacterController(CharacterDefinition definition, double startX, double startY)
    {
        _definition = definition;
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

    /// <summary>Lets MainWindow tell the controller where the OS-level DragMove() actually left the window.</summary>
    public void SyncPosition(double x, double y)
    {
        PositionX = x;
        PositionY = y;
    }

    public void BeginDrag()
    {
        _isFalling = false;
        TransitionTo(CharacterState.Dragging);
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

    /// <summary>Called by MainWindow once the (non-looping) wake animation finishes playing.</summary>
    public void OnWakeAnimationFinished()
    {
        if (State == CharacterState.Waking)
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

        if (_isFalling)
        {
            TickFalling(dt);
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
