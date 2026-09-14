using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches for removable drives (USB sticks, external HDDs/SSDs, an SD card
/// via a reader, ...) being plugged in or pulled out, via a short poll of
/// DriveInfo.GetDrives() rather than hooking WM_DEVICECHANGE - no window
/// handle or P/Invoke struct marshaling needed, and a couple of seconds of
/// latency is unnoticeable for a mascot's "ooh, what's this?" reaction.
/// Plain standard-user file-system enumeration the whole way through - like
/// every other watcher in this app, nothing here needs admin rights.
/// </summary>
public sealed class UsbDriveWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private HashSet<string> _lastSeenDrives = new();

    public event Action<string>? DriveConnected;
    public event Action<string>? DriveDisconnected;

    public void Start(CancellationToken cancellationToken)
    {
        _lastSeenDrives = GetReadyRemovableDrives();
        _ = PollLoopAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            RefreshOnce();
        }
    }

    private void RefreshOnce()
    {
        try
        {
            var current = GetReadyRemovableDrives();

            foreach (var drive in current)
            {
                if (!_lastSeenDrives.Contains(drive))
                {
                    DriveConnected?.Invoke(drive);
                }
            }

            foreach (var drive in _lastSeenDrives)
            {
                if (!current.Contains(drive))
                {
                    DriveDisconnected?.Invoke(drive);
                }
            }

            _lastSeenDrives = current;
        }
        catch (Exception ex)
        {
            ErrorLog.Record("UsbDriveWatcher.RefreshOnce", ex);
        }
    }

    private static HashSet<string> GetReadyRemovableDrives()
    {
        var result = new HashSet<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            // IsReady guards against a card reader/empty bay showing up as a
            // drive letter with nothing actually in it.
            if (drive.DriveType == DriveType.Removable && drive.IsReady)
            {
                result.Add(drive.Name);
            }
        }

        return result;
    }
}
