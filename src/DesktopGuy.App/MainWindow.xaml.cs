using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly BatteryWatcher _batteryWatcher = new();
    private readonly MeetingWatcher _meetingWatcher = new();
    private readonly ScreenshotWatcher _screenshotWatcher = new();
    private readonly FullscreenWatcher _fullscreenWatcher = new();
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly Stopwatch _clock = new();
    private readonly SingleInstanceGuard _instanceGuard;
    private DispatcherTimer? _speechHideTimer;
    private TrayIconController? _trayIcon;
    // null = automatic (follow the fullscreen watcher); true/false = the
    // tray icon's been left-clicked to force this state, which then wins
    // over the fullscreen watcher entirely until clicked again - otherwise
    // a false-positive fullscreen detection could never be manually
    // overridden (forcing "show" would do nothing if OR'd with an
    // fullscreen watcher that's stuck reporting true).
    private bool? _forcedVisible;

    public MainWindow(CharacterDefinition definition, SingleInstanceGuard instanceGuard)
    {
        InitializeComponent();

        _definition = definition;
        _instanceGuard = instanceGuard;
        StartWithWindowsMenuItem.IsChecked = StartupRegistration.IsEnabled();
        PopulateCharacterMenu();
        PopulateScaleMenu();

        var sheet = LoadSpriteSheet(definition);
        double windowWidth = definition.FrameSize.Width * definition.Scale;
        double windowHeight = definition.FrameSize.Height * definition.Scale;
        Width = windowWidth;
        Height = windowHeight;

        // Only the horizontal spot is remembered - he always starts resting
        // on the ground rather than wherever he happened to be lifted to.
        // The remembered X also decides which monitor's work area to use -
        // otherwise a second monitor would be permanently unreachable, and
        // a position saved there would snap back onto the primary
        // monitor's edge on every restart (see MonitorLayout).
        var remembered = PositionStore.TryLoad(definition.Id);
        var workArea = MonitorLayout.GetWorkAreaFor(remembered?.X);
        double minX = workArea.Left;
        double maxX = workArea.Right - windowWidth;
        double groundY = workArea.Bottom - windowHeight;

        double startX = remembered is { } p ? Math.Clamp(p.X, minX, maxX) : workArea.Right - windowWidth - 40;
        double startY = groundY;
        Left = startX;
        Top = startY;

        _animator = new SpriteAnimator(sheet, definition, "idle");
        _animator.AnimationCompleted += OnAnimatorAnimationCompleted;
        CharacterImage.Source = _animator.CurrentFrame;
        NextCharacterImage.Source = _animator.NextFrame;

        _controller = new CharacterController(
            definition, startX, startY, _mediaContext, _terminalWatcher, _typingWatcher, _batteryWatcher,
            _meetingWatcher, _screenshotWatcher);
        _controller.SetBounds(minX, maxX, groundY);
        _controller.StateChanged += OnControllerStateChanged;
        _controller.PositionChanged += OnControllerPositionChanged;
        _controller.SpeechRequested += ShowSpeech;

        _notificationWatcher.DiscordEventDetected += OnDiscordEventDetected;
        _screenshotWatcher.ScreenshotTaken += () => _controller.RequestSnapshotReaction();

        // Reuses the very first idle frame as the tray icon's picture -
        // whichever character is currently running is instantly
        // recognizable in the tray rather than a generic placeholder icon.
        _trayIcon = new TrayIconController(_animator.CurrentFrame, definition.DisplayName);
        _trayIcon.LeftClicked += OnTrayIconLeftClicked;
        _trayIcon.RightClicked += OnTrayIconRightClicked;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            Win32Interop.HideFromAltTabAndTaskbar(hwnd);
            _fullscreenWatcher.SetOwnWindowHandle(hwnd);
        };
        Closed += (_, _) =>
        {
            PositionStore.Save(_definition.Id, Left, Top);
            _controller.SaveOnscreenTime();
            _lifetimeCts.Cancel();
            _typingWatcher.Dispose();
            _screenshotWatcher.Dispose();
            _trayIcon?.Dispose();
            _instanceGuard.Dispose();
        };

        CompositionTarget.Rendering += OnRenderingFrame;
        _clock.Start();

        // Shown once, ever, regardless of which character or how many
        // times the app's been relaunched since - the right-click menu
        // isn't otherwise discoverable from just looking at him.
        if (OnboardingHints.ShouldShow("right-click-menu"))
        {
            OnboardingHints.MarkShown("right-click-menu");
            ShowSpeech("Right-click me anytime for options - characters, size, and more!");
        }

        // Context awareness talks to Windows over WinRT APIs and process
        // lists, which can quietly fail (unsupported OS build, permission
        // denied) - none of this blocks startup or the rest of the
        // character if it doesn't pan out.
        _ = _mediaContext.StartAsync(_lifetimeCts.Token);
        _ = StartNotificationWatcherAsync();
        _terminalWatcher.Start(_lifetimeCts.Token);
        _typingWatcher.Start();
        _batteryWatcher.Start(_lifetimeCts.Token);
        _meetingWatcher.Start(_lifetimeCts.Token);
        _screenshotWatcher.Start();
        _fullscreenWatcher.Start(_lifetimeCts.Token);
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

        UpdateVisibility();
    }

    /// <summary>
    /// Hides the window - rather than closing it, everything (Tick,
    /// onscreen-time accrual, the tray icon) keeps running underneath -
    /// while a fullscreen app has taken over the screen, unless the tray
    /// icon's been used to force a particular state instead (see
    /// _forcedVisible). Also closes the speech bubble on the way out so it
    /// doesn't linger alone over whatever's now fullscreen.
    /// </summary>
    private void UpdateVisibility()
    {
        bool shouldShow = _forcedVisible ?? !_fullscreenWatcher.IsActive;
        bool isVisible = Visibility == Visibility.Visible;

        if (shouldShow == isVisible)
        {
            return;
        }

        Visibility = shouldShow ? Visibility.Visible : Visibility.Hidden;
        if (!shouldShow)
        {
            SpeechPopup.IsOpen = false;
        }
    }

    /// <summary>
    /// Forces the opposite of whatever's currently showing - including
    /// overriding a fullscreen-watcher false positive that would otherwise
    /// make a plain hide/show toggle a no-op (setting "show" while the
    /// watcher still insists it's fullscreen would never actually reveal
    /// him if this only flipped a flag that gets OR'd with the watcher).
    /// The forced state then sticks until clicked again, regardless of
    /// what the fullscreen watcher reports in the meantime.
    /// </summary>
    private void OnTrayIconLeftClicked()
    {
        bool currentlyVisible = _forcedVisible ?? !_fullscreenWatcher.IsActive;
        _forcedVisible = !currentlyVisible;
    }

    private void OnTrayIconRightClicked()
    {
        var menu = ContextMenu;
        if (menu is null)
        {
            return;
        }

        menu.PlacementTarget = this;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
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
            case "snapshot":
            case "eating":
            case "playing":
            case "lowBattery":
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

    /// <summary>
    /// Starts Discord awareness, then - only the very first time this ever
    /// happens across the app's whole lifetime, not per character or per
    /// restart - nudges about it with a speech bubble if it *didn't* come
    /// up active. That covers both "never granted" (the OS only prompts
    /// once; a dismissal or denial is remembered and never re-asked) and
    /// "unsupported on this system" - either way, it's otherwise a
    /// silently-missing feature nobody would know to look for.
    /// </summary>
    private async Task StartNotificationWatcherAsync()
    {
        bool active = await _notificationWatcher.StartAsync(_lifetimeCts.Token);
        if (!active && OnboardingHints.ShouldShow("discord-notifications"))
        {
            OnboardingHints.MarkShown("discord-notifications");
            ShowSpeech("Psst - I can react to your Discord calls and messages too, if you allow notification access in Windows settings!");
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

        double startLeft = Left;
        double startTop = Top;

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

        // DragMove() is also what fires for a plain click (it just never
        // actually moves anything in that case) - if the window ended up
        // within a couple pixels of where it started, treat it as a pet
        // rather than a drag.
        bool actuallyDragged = Math.Abs(Left - startLeft) > 2 || Math.Abs(Top - startTop) > 2;
        if (!actuallyDragged)
        {
            _controller.OnPetted();
        }

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

        // Released before launching the replacement rather than left for
        // this window's Closed handler - the new process (a different
        // character, so a different lock name here, but kept consistent
        // with the other two relaunch sites below) shouldn't have to race
        // this process's actual shutdown to acquire its own lock.
        _instanceGuard.Release();

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

        double[] percentages = { 0.5, 0.75, 0.9, 1.0, 1.1, 1.25, 1.5 };
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

        // This relaunch targets the *same* character folder as this
        // instance - without releasing the lock first, the new process
        // would race this one's actual shutdown for the same named mutex
        // and likely lose, treating itself as a spurious duplicate.
        _instanceGuard.Release();

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

    /// <summary>
    /// Opens the "New Character" dialog. A successful import doesn't touch
    /// this running instance at all (new folder on disk only) - offering to
    /// relaunch into it reuses the same switch-character mechanism as the
    /// Character submenu.
    /// </summary>
    private void OnNewCharacterClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new NewCharacterWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.ImportedFolderName is not { } folderName)
        {
            return;
        }

        var result = MessageBox.Show(
            $"\"{folderName}\" was created. Switch to it now?",
            "Desktop Guy",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        CharacterPreferenceStore.Save(folderName);
        _instanceGuard.Release();

        string? exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath, $"--character \"{folderName}\"")
            {
                UseShellExecute = true,
            });
        }

        Application.Current.Shutdown();
    }

    private void OnPreviewWeatherClicked(object sender, RoutedEventArgs e)
    {
        var condition = Enum.Parse<WeatherCondition>((string)((MenuItem)sender).Tag);
        _controller.PreviewWeather(condition);
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

    private void OnViewErrorLogClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = ErrorLog.GetOrCreatePath();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Couldn't open the error log:\n{ex.Message}",
                "Desktop Guy",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Wipes all remembered state (position, scale, character choice,
    /// onscreen time, pet milestones, the error log, which onboarding
    /// hints have shown) and turns off "Start with Windows", then
    /// restarts fresh into whatever character comes up first - the same
    /// zero-config state as a brand new install.
    /// </summary>
    private void OnResetAllSettingsClicked(object sender, RoutedEventArgs e)
    {
        var confirmed = MessageBox.Show(
            "This clears everything the app remembers - position, size, which " +
            "character you last picked, onscreen-time progress, and the " +
            "startup setting - and restarts fresh. This can't be undone. Continue?",
            "Reset All Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmed != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            AppDataReset.ResetAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Couldn't fully reset:\n{ex.Message}",
                "Desktop Guy",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _instanceGuard.Release();

        string? exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
        }

        Application.Current.Shutdown();
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
