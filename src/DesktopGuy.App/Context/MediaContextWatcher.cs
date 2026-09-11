using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;
using Windows.Media;
using Windows.Media.Control;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches Windows' built-in "now playing" system (the same one that
/// powers the media transport controls on the lock screen and volume
/// flyout) to notice when something is actively playing, and whether it
/// looks like music or a video - Spotify, YouTube Music, a YouTube tab,
/// Netflix, etc. all report through this without us needing to know
/// anything about the specific app.
///
/// This is a WinRT API. It works from an unpackaged Win32 app, but if it's
/// unavailable for any reason (old Windows version, locked-down system),
/// this degrades quietly to always reporting <see cref="MediaPlaybackContext.None"/>
/// rather than crashing the app.
/// </summary>
public sealed class MediaContextWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

    // Browsers frequently don't report a PlaybackType at all for HTML5
    // video (Chromium-based ones especially), so when Windows doesn't tell
    // us, we take a guess from the focused window's title as a tiebreaker.
    private static readonly string[] VideoSiteTitleMarkers =
    {
        "YouTube", "Netflix", "Twitch", "Prime Video", "Disney+", "Hulu", "HBO Max",
    };

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private volatile MediaPlaybackContext _current = MediaPlaybackContext.None;

    public MediaPlaybackContext Current => _current;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch
        {
            _manager = null;
            return;
        }

        _ = PollLoopAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            RefreshOnce();
            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private void RefreshOnce()
    {
        if (_manager is null)
        {
            _current = MediaPlaybackContext.None;
            return;
        }

        try
        {
            var session = _manager.GetCurrentSession();
            if (session is null)
            {
                _current = MediaPlaybackContext.None;
                return;
            }

            var playbackInfo = session.GetPlaybackInfo();
            if (playbackInfo?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                _current = MediaPlaybackContext.None;
                return;
            }

            _current = playbackInfo.PlaybackType switch
            {
                MediaPlaybackType.Video => MediaPlaybackContext.Video,
                MediaPlaybackType.Music => MediaPlaybackContext.Music,
                _ => GuessFromForegroundWindowTitle(),
            };
        }
        catch
        {
            // A session can disappear between the null-check and reading it
            // (app closed mid-poll); just wait for the next tick.
            _current = MediaPlaybackContext.None;
        }
    }

    /// <summary>
    /// Windows didn't tell us whether this is music or video - take a guess
    /// from the currently focused window's title (e.g. a browser tab titled
    /// "... - YouTube"). Falls back to music, the more common case for an
    /// untyped session (background audio players, Spotify's web player, etc.).
    /// </summary>
    private static MediaPlaybackContext GuessFromForegroundWindowTitle()
    {
        string title = Win32Interop.GetForegroundWindowTitle();
        bool looksLikeVideo = VideoSiteTitleMarkers.Any(
            marker => title.Contains(marker, StringComparison.OrdinalIgnoreCase));
        return looksLikeVideo ? MediaPlaybackContext.Video : MediaPlaybackContext.Music;
    }
}
