using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches free space on the drive the app itself is running from, so a
/// nearly-full disk can get a one-off nudge (see CharacterController's
/// disk space check) - same polling shape as BatteryWatcher, since disk
/// usage changes slowly enough that anything faster would just waste
/// cycles.
/// </summary>
public sealed class DiskSpaceWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private const long LowSpaceBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    private volatile bool _isLow;

    /// <summary>True when the app's drive has less than LowSpaceBytes free.</summary>
    public bool IsLow => _isLow;

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
            string? root = Path.GetPathRoot(AppContext.BaseDirectory);
            if (string.IsNullOrEmpty(root))
            {
                _isLow = false;
                return;
            }

            var drive = new DriveInfo(root);
            _isLow = drive.IsReady && drive.AvailableFreeSpace < LowSpaceBytes;
        }
        catch (Exception ex)
        {
            ErrorLog.Record("DiskSpaceWatcher.RefreshOnce", ex);
            _isLow = false;
        }
    }
}
