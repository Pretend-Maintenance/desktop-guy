using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when a video-call app (Zoom, Microsoft Teams, Google Meet, ...)
/// is the window you're actually focused on right now. Reuses the
/// "watching" animation (see CharacterController) rather than needing its
/// own dedicated art - "staring at a video call" and "watching a video"
/// read as the same pose. Native apps are matched by process name; Google
/// Meet (and Zoom/Teams when used in-browser instead of the native app)
/// only shows up as a browser tab, so the focused window's title is also
/// checked for a marker, the same approach MediaContextWatcher uses for
/// video sites.
/// </summary>
public sealed class MeetingWatcher
{
    private static readonly string[] ProcessNames =
    {
        "Zoom", "Teams", "ms-teams", "Skype", "SkypeApp", "webexmta", "GoToMeeting",
    };

    private static readonly string[] TitleMarkers =
    {
        "Meet - ", "Google Meet", "Zoom Meeting", "Microsoft Teams",
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

    private volatile bool _isActive;

    public bool IsActive => _isActive;

    public void Start(CancellationToken cancellationToken)
    {
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
        try
        {
            int? processId = Win32Interop.GetForegroundProcessId();
            if (processId is null)
            {
                _isActive = false;
                return;
            }

            using var process = Process.GetProcessById(processId.Value);
            bool isKnownApp = ProcessNames.Any(
                name => string.Equals(name, process.ProcessName, StringComparison.OrdinalIgnoreCase));

            if (isKnownApp)
            {
                _isActive = true;
                return;
            }

            string title = Win32Interop.GetForegroundWindowTitle();
            _isActive = TitleMarkers.Any(marker => title.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // The foreground process can disappear between the lookup and
            // reading it (window closed mid-poll); just wait for the next tick.
            _isActive = false;
        }
    }
}
