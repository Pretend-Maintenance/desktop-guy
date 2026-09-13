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

    // Browsers can't always be trusted to report the right PlaybackType for
    // HTML5 video - some don't report one at all, others (Firefox) have
    // been seen reporting the wrong one - so a focused window titled after
    // a known video site is checked first, ahead of the self-reported type.
    private static readonly string[] VideoSiteTitleMarkers =
    {
        "YouTube", "Netflix", "Twitch", "Prime Video", "Disney+", "Hulu", "HBO Max",
    };

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly AudioLevelWatcher _audioLevel = new();
    private volatile MediaPlaybackContext _current = MediaPlaybackContext.None;
    private volatile string? _currentTitle;
    private volatile string? _currentArtist;

    public MediaPlaybackContext Current => _current;

    /// <summary>
    /// The track/video title self-reported by whatever's currently playing,
    /// if anything - null whenever Current is None, or if the app just
    /// didn't report one (some browser tabs don't). Best-effort only, used
    /// for an occasional "now playing" speech bubble.
    /// </summary>
    public string? CurrentTitle => _currentTitle;

    /// <summary>The artist/uploader self-reported alongside CurrentTitle, if any.</summary>
    public string? CurrentArtist => _currentArtist;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Starts regardless of whether the media-session manager below is
        // available, since it's an independent signal (used to catch a
        // muted tab or muted speakers still reporting PlaybackStatus.Playing).
        _audioLevel.Start(cancellationToken);

        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        }
        catch (Exception ex)
        {
            ErrorLog.Record("MediaContextWatcher.StartAsync", ex);
            _manager = null;
            return;
        }

        _ = PollLoopAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshOnceAsync();
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

    private async Task RefreshOnceAsync()
    {
        if (_manager is null)
        {
            _current = MediaPlaybackContext.None;
            _currentTitle = null;
            _currentArtist = null;
            return;
        }

        try
        {
            var session = FindPlayingSession();
            var playbackInfo = session?.GetPlaybackInfo();
            if (playbackInfo?.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                _current = MediaPlaybackContext.None;
                _currentTitle = null;
                _currentArtist = null;
                return;
            }

            // A muted tab or muted speakers still reports PlaybackStatus.Playing
            // - checking the actual output level catches both, so he doesn't
            // keep dancing/watching along to something you can't hear.
            if (!_audioLevel.IsAudible)
            {
                _current = MediaPlaybackContext.None;
                _currentTitle = null;
                _currentArtist = null;
                return;
            }

            // The focused window's title is checked first, ahead of
            // whatever the app itself reports: some browsers (Firefox in
            // particular) have been seen reporting an incorrect type
            // (e.g. Music for an actual YouTube video), not just an absent
            // one - so a clear video-site title in the title bar is trusted
            // over a possibly-wrong self-reported type.
            _current = IsFocusedOnVideoSite() || playbackInfo.PlaybackType == MediaPlaybackType.Video
                ? MediaPlaybackContext.Video
                : MediaPlaybackContext.Music;

            try
            {
                var properties = await session!.TryGetMediaPropertiesAsync();
                _currentTitle = string.IsNullOrWhiteSpace(properties?.Title) ? null : properties.Title;
                _currentArtist = string.IsNullOrWhiteSpace(properties?.Artist) ? null : properties.Artist;
            }
            catch
            {
                // Title/artist are a nice-to-have for the "now playing"
                // speech bubble - not worth losing the play/pause state
                // over if this particular call fails.
                _currentTitle = null;
                _currentArtist = null;
            }
        }
        catch
        {
            // A session can disappear between finding it and reading it
            // (app closed mid-poll); just wait for the next tick.
            _current = MediaPlaybackContext.None;
            _currentTitle = null;
            _currentArtist = null;
        }
    }

    /// <summary>
    /// GetCurrentSession() only returns Windows' notion of the single
    /// "most relevant" session, which isn't always the one that's actually
    /// playing (e.g. a paused/idle app can outrank a browser tab that's
    /// genuinely playing). Checking every session and picking whichever one
    /// is actually Playing is more reliable when more than one app has a
    /// registered media session at once.
    /// </summary>
    private GlobalSystemMediaTransportControlsSession? FindPlayingSession()
    {
        foreach (var session in _manager!.GetSessions())
        {
            if (session.GetPlaybackInfo()?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                return session;
            }
        }

        return null;
    }

    private static bool IsFocusedOnVideoSite()
    {
        string title = Win32Interop.GetForegroundWindowTitle();
        return VideoSiteTitleMarkers.Any(marker => title.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
