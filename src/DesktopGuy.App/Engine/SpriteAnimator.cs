using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using DesktopGuy.App.Characters;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Plays one row of a sprite sheet at a time by cropping out the current
/// frame's rectangle. Character-agnostic: it only knows about the
/// CharacterDefinition's frame size and animation rows.
/// </summary>
public sealed class SpriteAnimator : INotifyPropertyChanged
{
    private readonly BitmapSource _sheet;
    private readonly CharacterDefinition _definition;

    private AnimationDefinition _current;
    private string _currentName;
    private int _frameIndex;
    private double _secondsAccumulated;
    private BitmapSource _currentFrame;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised once when a non-looping animation finishes its last frame.</summary>
    public event Action? AnimationCompleted;

    public SpriteAnimator(BitmapSource sheet, CharacterDefinition definition, string initialAnimation)
    {
        _sheet = sheet;
        _definition = definition;
        _currentName = initialAnimation;
        _current = ResolveAnimation(initialAnimation);
        _currentFrame = CropFrame(_current.Row, 0);
    }

    public BitmapSource CurrentFrame
    {
        get => _currentFrame;
        private set
        {
            _currentFrame = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFrame)));
        }
    }

    public string CurrentAnimationName => _currentName;

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
        CurrentFrame = CropFrame(_current.Row, 0);
    }

    public void Tick(TimeSpan elapsed)
    {
        if (_current.FrameCount <= 1 || _current.Fps <= 0)
        {
            return;
        }

        _secondsAccumulated += elapsed.TotalSeconds;
        double secondsPerFrame = 1.0 / _current.Fps;

        if (_secondsAccumulated < secondsPerFrame)
        {
            return;
        }

        _secondsAccumulated -= secondsPerFrame;
        int nextIndex = _frameIndex + 1;

        if (nextIndex >= _current.FrameCount)
        {
            if (_current.Loop)
            {
                nextIndex = 0;
            }
            else
            {
                _frameIndex = _current.FrameCount - 1;
                CurrentFrame = CropFrame(_current.Row, _frameIndex);
                AnimationCompleted?.Invoke();
                return;
            }
        }

        _frameIndex = nextIndex;
        CurrentFrame = CropFrame(_current.Row, _frameIndex);
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
