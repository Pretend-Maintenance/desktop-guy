using System;
using System.Diagnostics;
using System.Globalization;
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
    private readonly TerminalWatcher _terminalWatcher = new();
    private readonly TypingWatcher _typingWatcher = new();
    private readonly WeatherWatcher _weatherWatcher;
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _speechHideTimer;

    public MainWindow(CharacterDefinition definition)
    {
        InitializeComponent();

        _definition = definition;
        StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled();
        PopulateCharacterMenu();
        PopulateScaleMenu();

        var sheet = LoadSpriteSheet(definition);
        double windowWidth = definition.FrameSize.Width * definition.Scale;
        double windowHeight = definition.FrameSize.Height * definition.Scale;
        Width = windowWidth;
        Height = windowHeight;

        var workArea = SystemParameters.WorkArea;
        double minX = workArea.Left;
        double maxX = workArea.Right - windowWidth;
        double groundY = workArea.Bottom - windowHeight;

        // Only the horizontal spot is remembered - he always starts resting
        // on the ground rather than wherever he happened to be lifted to.
        var remembered = PositionStore.TryLoad(definition.Id);
        double startX = remembered is { } p ? Math.Clamp(p.X, minX, maxX) : workArea.Right - windowWidth - 40;
        double startY = groundY;
        Left = startX;
        Top = startY;

        _animator = new SpriteAnimator(sheet, definition, "idle");
        _animator.AnimationCompleted += OnAnimatorAnimationCompleted;
        CharacterImage.Source = _animator.CurrentFrame;
        NextCharacterImage.Source = _animator.NextFrame;

        _weatherWatcher = new WeatherWatcher(
            definition.Behavior.ColdThresholdCelsius, definition.Behavior.HotThresholdCelsius);

        _controller = new CharacterController(
            definition, startX, startY, _mediaContext, _terminalWatcher, _typingWatcher, _weatherWatcher);
        _controller.SetBounds(minX, maxX, groundY);
        _controller.StateChanged += OnControllerStateChanged;
        _controller.PositionChanged += OnControllerPositionChanged;
        _controller.SpeechRequested += ShowSpeech;

        _notificationWatcher.DiscordEventDetected += OnDiscordEventDetected;

        SourceInitialized += (_, _) =>
            Win32Interop.HideFromAltTabAndTaskbar(new WindowInteropHelper(this).Handle);
        Closed += (_, _) =>
        {
            PositionStore.Save(_definition.Id, Left, Top);
            _lifetimeCts.Cancel();
            _typingWatcher.Dispose();
        };

        CompositionTarget.Rendering += OnRenderingFrame;
        _clock.Start();

        // Context awareness talks to Windows over WinRT APIs and process
        // lists, which can quietly fail (unsupported OS build, permission
        // denied) - none of this blocks startup or the rest of the
        // character if it doesn't pan out.
        _ = _mediaContext.StartAsync(_lifetimeCts.Token);
        _ = _notificationWatcher.StartAsync(_lifetimeCts.Token);
        _terminalWatcher.Start(_lifetimeCts.Token);
        _typingWatcher.Start();
        _weatherWatcher.Start(_lifetimeCts.Token);
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
        double facingScaleX = _controller.FacingRight ? 1 : -1;
        FacingTransform.ScaleX = facingScaleX;
        NextFacingTransform.ScaleX = facingScaleX;

        CharacterImage.Source = _animator.CurrentFrame;
        NextCharacterImage.Source = _animator.NextFrame;
        NextCharacterImage.Opacity = _definition.SmoothTransitions ? _animator.BlendProgress : 0;
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
            case "pickUp":
                // The startled "just grabbed" animation finished - settle
                // into the ongoing drag wobble for as long as the drag lasts.
                if (_controller.State == CharacterState.Dragging)
                {
                    _animator.Play(CharacterAnimationMap.GetAnimationName(CharacterState.Dragging));
                }
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
        // Grabbing him plays a brief startled "pickUp" animation first (if
        // the character defines one), then bridges into the drag loop via
        // OnAnimatorAnimationCompleted above.
        if (state == CharacterState.Dragging && _animator.HasAnimation("pickUp"))
        {
            _animator.Play("pickUp");
            return;
        }

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

        try
        {
            // Window.DragMove() hands the drag off to Windows itself, so
            // there's no cursor-to-window coordinate math for us to get
            // wrong (a manual mouse-capture version of this previously
            // sent the window to an invalid position on some displays).
            // The tradeoff: it blocks this thread for the whole drag, so
            // the pickUp/drag animation won't visibly play through its
            // frames while held - the first frame still shows correctly.
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

    /// <summary>
    /// Fills the "Character" submenu with one checkable entry per character
    /// folder under Assets/Characters, ticking whichever one is currently
    /// running.
    /// </summary>
    private void PopulateCharacterMenu()
    {
        string currentFolderName = Path.GetFileName(_definition.SourceFolder);

        foreach (var folderName in CharacterLoader.DiscoverCharacterIds())
        {
            string displayName;
            try
            {
                displayName = CharacterLoader.Load(folderName).DisplayName;
            }
            catch
            {
                // A folder with a broken character.json shouldn't block the
                // rest of the menu from being usable.
                continue;
            }

            var item = new MenuItem
            {
                Header = displayName,
                IsCheckable = true,
                IsChecked = string.Equals(folderName, currentFolderName, StringComparison.OrdinalIgnoreCase),
                Tag = folderName,
            };
            item.Click += OnCharacterSelected;
            CharacterMenuItem.Items.Add(item);
        }
    }

    /// <summary>
    /// Switching characters mid-run would mean rebuilding the sprite sheet,
    /// controller, and every context watcher for a different frame size and
    /// animation set - simpler and more robust to just relaunch the whole
    /// app pointed at the new character and let this instance exit.
    /// </summary>
    private void OnCharacterSelected(object sender, RoutedEventArgs e)
    {
        var item = (MenuItem)sender;
        var folderName = (string)item.Tag;

        if (string.Equals(folderName, Path.GetFileName(_definition.SourceFolder), StringComparison.OrdinalIgnoreCase))
        {
            item.IsChecked = true;
            return;
        }

        CharacterPreferenceStore.Save(folderName);

        // Keep the "Start with Windows" registration pointed at whatever's
        // actually selected, if it's on - otherwise it'd keep launching the
        // character you just switched away from.
        if (StartupRegistration.IsEnabled())
        {
            try
            {
                StartupRegistration.SetEnabled(true, folderName);
            }
            catch
            {
                // Not worth blocking the switch over - it'll just launch
                // the old character next boot, same as before this click.
            }
        }

        string? exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath, $"--character \"{folderName}\"")
            {
                UseShellExecute = true,
            });
        }

        // The window's Closed handler already saves position and tears
        // down the watchers - Shutdown() triggers that the same as any
        // other close.
        Application.Current.Shutdown();
    }

    /// <summary>
    /// Fills the "Size" submenu with a handful of percentages of this
    /// character's own authored default scale (character.json's "scale" -
    /// re-loaded fresh here so the percentages stay anchored to that
    /// original value even after a previous resize), ticking whichever one
    /// is currently active.
    /// </summary>
    private void PopulateScaleMenu()
    {
        string folderName = Path.GetFileName(_definition.SourceFolder);
        double baseScale;
        try
        {
            baseScale = CharacterLoader.Load(folderName).Scale;
        }
        catch
        {
            baseScale = _definition.Scale;
        }

        double[] percentages = { 0.75, 0.9, 1.0, 1.1, 1.25, 1.5 };
        foreach (var percentage in percentages)
        {
            double candidateScale = Math.Round(baseScale * percentage, 3);
            var item = new MenuItem
            {
                Header = percentage == 1.0 ? "100% (default)" : $"{percentage * 100:0}%",
                IsCheckable = true,
                IsChecked = Math.Abs(_definition.Scale - candidateScale) < 0.01,
                Tag = candidateScale,
            };
            item.Click += OnScaleSelected;
            ScaleMenuItem.Items.Add(item);
        }
    }

    /// <summary>
    /// Like OnCharacterSelected, resizing live would mean recomputing the
    /// controller's movement bounds and repositioning the window to keep
    /// him glued to the ground rather than just growing/shrinking from a
    /// corner - relaunching at the new scale sidesteps all of that.
    /// </summary>
    private void OnScaleSelected(object sender, RoutedEventArgs e)
    {
        var item = (MenuItem)sender;
        var newScale = (double)item.Tag;

        if (Math.Abs(newScale - _definition.Scale) < 0.01)
        {
            item.IsChecked = true;
            return;
        }

        string folderName = Path.GetFileName(_definition.SourceFolder);
        ScalePreferenceStore.Save(folderName, newScale);

        string? exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(
                exePath,
                $"--character \"{folderName}\" --scale {newScale.ToString(CultureInfo.InvariantCulture)}")
            {
                UseShellExecute = true,
            });
        }

        Application.Current.Shutdown();
    }

    private void OnStartWithWindowsToggled(object sender, RoutedEventArgs e)
    {
        try
        {
            StartupRegistration.SetEnabled(StartWithWindowsMenuItem.IsChecked, _definition.Id);
        }
        catch (Exception ex)
        {
            StartWithWindowsMenuItem.IsChecked = !StartWithWindowsMenuItem.IsChecked;
            MessageBox.Show(
                $"Couldn't update the startup setting:\n{ex.Message}",
                "Desktop Guy",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
