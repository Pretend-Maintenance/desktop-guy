using System;
using System.Windows.Media.Imaging;
using DesktopGuy.App.Characters;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Plays one row of a sprite sheet at a time by cropping out the current
/// frame's rectangle. Character-agnostic: it only knows about the
/// CharacterDefinition's frame size and animation rows.
///
/// Also exposes the *next* frame and a 0-1 blend progress toward it every
/// tick, so MainWindow can optionally cross-fade between frames instead of
/// hard-cutting - a way to make a handful of sprite-sheet poses read as
/// smoother motion without needing more art. Whether that's actually used
/// is up to the character (see CharacterDefinition.SmoothTransitions) -
/// blocky pixel art usually wants the hard cut, smoother-shaded art
/// usually looks better cross-fading.
/// </summary>
public sealed class SpriteAnimator
{
    private readonly BitmapSource _sheet;
    private readonly CharacterDefinition _definition;

    private AnimationDefinition _current;
    private string _currentName;
    private int _frameIndex;
    private double _secondsAccumulated;

    /// <summary>Raised once when a non-looping animation finishes its last frame.</summary>
    public event Action? AnimationCompleted;

    public SpriteAnimator(BitmapSource sheet, CharacterDefinition definition, string initialAnimation)
    {
        _sheet = sheet;
        _definition = definition;
        _currentName = initialAnimation;
        _current = ResolveAnimation(initialAnimation);
        UpdateFrames();
    }

    public BitmapSource CurrentFrame { get; private set; } = null!;
    public BitmapSource NextFrame { get; private set; } = null!;

    /// <summary>0 at the start of the current frame, approaching 1 just before it advances to NextFrame.</summary>
    public double BlendProgress { get; private set; }

    public string CurrentAnimationName => _currentName;

    public bool HasAnimation(string animationName) => _definition.Animations.ContainsKey(animationName);

    public void Play(string animationName, bool restartIfSame = false)
    {
        if (!restartIfSame && animationName == _currentName)
        {
            return;
        }

        _currentName = animationName;
        _current = ResolveAnimation(animationName);
        _frameIndex = 0;
        _secondsAccumulated = 0;
        BlendProgress = 0;
        UpdateFrames();
    }

    public void Tick(TimeSpan elapsed)
    {
        if (_current.FrameCount <= 1 || _current.Fps <= 0)
        {
            BlendProgress = 0;
            return;
        }

        double secondsPerFrame = 1.0 / _current.Fps;
        _secondsAccumulated += elapsed.TotalSeconds;

        while (_secondsAccumulated >= secondsPerFrame)
        {
            _secondsAccumulated -= secondsPerFrame;
            if (!AdvanceFrame())
            {
                // Held on the last frame of a non-looping animation - stop
                // accumulating so this loop can't spin forever.
                _secondsAccumulated = 0;
                break;
            }
        }

        BlendProgress = Math.Clamp(_secondsAccumulated / secondsPerFrame, 0, 1);
    }

    /// <summary>Advances to the next frame. Returns false if held on the final frame of a non-looping animation.</summary>
    private bool AdvanceFrame()
    {
        int nextIndex = _frameIndex + 1;

        if (nextIndex >= _current.FrameCount)
        {
            if (_current.Loop)
            {
                nextIndex = 0;
            }
            else
            {
                UpdateFrames();
                AnimationCompleted?.Invoke();
                return false;
            }
        }

        _frameIndex = nextIndex;
        UpdateFrames();
        return true;
    }

    private void UpdateFrames()
    {
        CurrentFrame = CropFrame(_current.Row, _frameIndex);
        NextFrame = CropFrame(_current.Row, PeekNextIndex());
    }

    private int PeekNextIndex()
    {
        int next = _frameIndex + 1;
        if (next < _current.FrameCount)
        {
            return next;
        }

        return _current.Loop ? 0 : _frameIndex;
    }

    private AnimationDefinition ResolveAnimation(string name)
    {
        if (_definition.Animations.TryGetValue(name, out var animation))
        {
            return animation;
        }

        throw new InvalidOperationException(
            $"Character '{_definition.DisplayName}' has no '{name}' animation defined in character.json.");
    }

    private BitmapSource CropFrame(int row, int column)
    {
        int width = _definition.FrameSize.Width;
        int height = _definition.FrameSize.Height;
        var rect = new System.Windows.Int32Rect(column * width, row * height, width, height);
        return new CroppedBitmap(_sheet, rect);
    }
}
