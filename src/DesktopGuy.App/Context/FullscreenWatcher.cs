using System;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when some other app has gone exclusively fullscreen (a game, a
/// video player, a presentation) so MainWindow can hide the character
/// rather than sit on top of it. Same rough "focused window covers the
/// whole monitor" heuristic several taskbar-autohide-style tools use - see
/// Win32Interop.IsForegroundWindowFullscreen for the actual check.
/// </summary>
public sealed class FullscreenWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private IntPtr _ownHwnd = IntPtr.Zero;
    private volatile bool _isActive;

    public bool IsActive => _isActive;

    /// <summary>Must be called once the window handle exists (e.g. from SourceInitialized) so the check can exclude our own window.</summary>
    public void SetOwnWindowHandle(IntPtr hwnd) => _ownHwnd = hwnd;

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
            _isActive = Win32Interop.IsForegroundWindowFullscreen(_ownHwnd);
        }
        catch (Exception ex)
        {
            ErrorLog.Record("FullscreenWatcher.RefreshOnce", ex);
            _isActive = false;
        }
    }
}
