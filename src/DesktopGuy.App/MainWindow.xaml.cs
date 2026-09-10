using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopGuy.App.Characters;
using DesktopGuy.App.Context;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App;

public partial class MainWindow : Window
{
    private readonly CharacterDefinition _definition;
    private readonly SpriteAnimator _animator;
    private readonly CharacterController _controller;
    private readonly MediaContextWatcher _mediaContext = new();
    private readonly NotificationWatcher _notificationWatcher = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _speechHideTimer;

    public MainWindow(CharacterDefinition definition)
    {
        InitializeComponent();

        _definition = definition;

        var sheet = LoadSpriteSheet(definition);
        double windowWidth = definition.FrameSize.Width * definition.Scale;
        double windowHeight = definition.FrameSize.Height * definition.Scale;
        Width = windowWidth;
        Height = windowHeight;

        var workArea = SystemParameters.WorkArea;
        double startX = workArea.Right - windowWidth - 40;
        double startY = workArea.Bottom - windowHeight;
        Left = startX;
        Top = startY;

        _animator = new SpriteAnimator(sheet, definition, "idle");
        _animator.PropertyChanged += (_, _) => CharacterImage.Source = _animator.CurrentFrame;
        _animator.AnimationCompleted += OnAnimatorAnimationCompleted;
        CharacterImage.Source = _animator.CurrentFrame;

        _controller = new CharacterController(definition, startX, startY, _mediaContext);
        _controller.SetBounds(workArea.Left, workArea.Right - windowWidth, workArea.Bottom - windowHeight);
        _controller.StateChanged += OnControllerStateChanged;
        _controller.PositionChanged += OnControllerPositionChanged;
        _controller.SpeechRequested += ShowSpeech;

        _notificationWatcher.DiscordEventDetected += OnDiscordEventDetected;

        SourceInitialized += (_, _) =>
            Win32Interop.HideFromAltTabAndTaskbar(new WindowInteropHelper(this).Handle);
        Closed += (_, _) => _lifetimeCts.Cancel();

        CompositionTarget.Rendering += OnRenderingFrame;
        _clock.Start();

        // Context awareness talks to Windows over WinRT APIs, which are
        // inherently async and can quietly fail (unsupported OS build,
        // permission denied) - none of this blocks startup or the rest of
        // the character if it doesn't pan out.
        _ = _mediaContext.StartAsync(_lifetimeCts.Token);
        _ = _notificationWatcher.StartAsync(_lifetimeCts.Token);
    }

    private static BitmapImage LoadSpriteSheet(CharacterDefinition definition)
    {
        var path = Path.Combine(definition.SourceFolder, definition.SpriteSheet);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private TimeSpan _lastFrameTime;

    private void OnRenderingFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = _lastFrameTime == TimeSpan.Zero ? TimeSpan.Zero : now - _lastFrameTime;
        _lastFrameTime = now;

        // Guard against huge jumps (e.g. the app was suspended/minimized).
        if (elapsed > TimeSpan.FromMilliseconds(250))
        {
            elapsed = TimeSpan.FromMilliseconds(16);
        }

        _controller.Tick(elapsed);
        _animator.Tick(elapsed);
        FacingTransform.ScaleX = _controller.FacingRight ? 1 : -1;
    }

    private void OnAnimatorAnimationCompleted()
    {
        switch (_animator.CurrentAnimationName)
        {
            case "wake":
                _controller.OnWakeAnimationFinished();
                break;
            case "answerCall":
            case "openMail":
                _controller.OnReactionAnimationFinished();
                break;
        }
    }

    private void OnDiscordEventDetected(DiscordEvent discordEvent)
    {
        // NotificationWatcher polls on a background task - hop back to the
        // UI thread before touching the controller/window.
        Dispatcher.Invoke(() => _controller.RequestReaction(discordEvent));
    }

    private void OnControllerStateChanged(CharacterState state)
    {
        _animator.Play(CharacterAnimationMap.GetAnimationName(state));
    }

    private void OnControllerPositionChanged()
    {
        Left = _controller.PositionX;
        Top = _controller.PositionY;
    }

    private void OnCharacterMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _controller.BeginDrag();
        _animator.Play(CharacterAnimationMap.GetAnimationName(CharacterState.Dragging));

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Mouse was released before the OS-level drag actually started.
        }

        _controller.SyncPosition(Left, Top);
        _controller.EndDrag();
        e.Handled = true;
    }

    private void ShowSpeech(string text)
    {
        SpeechText.Text = text;
        SpeechPopup.IsOpen = true;

        _speechHideTimer?.Stop();
        _speechHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_definition.Behavior.SpeechDurationSeconds),
        };
        _speechHideTimer.Tick += (_, _) =>
        {
            SpeechPopup.IsOpen = false;
            _speechHideTimer!.Stop();
        };
        _speechHideTimer.Start();
    }

    private void OnSayHiClicked(object sender, RoutedEventArgs e)
    {
        if (_definition.Phrases.Count > 0)
        {
            ShowSpeech(_definition.Phrases[new Random().Next(_definition.Phrases.Count)]);
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
