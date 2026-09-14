using System;
using Microsoft.Win32;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when the whole PC resumes from sleep or hibernation - a
/// different signal from the idle-timeout-based Sleeping/Waking pair
/// CharacterController already handles, which tracks *you* stepping away
/// while the PC stays running. This one only fires on an actual OS-level
/// suspend/resume cycle, via the same static event Windows itself raises
/// for power state changes - no polling needed. Subscribing is the same
/// as starting; Dispose() unsubscribes (SystemEvents holds this as a
/// static reference, so forgetting to unsubscribe would leak the window).
/// </summary>
public sealed class SystemResumeWatcher : IDisposable
{
    public event Action? Resumed;

    public SystemResumeWatcher()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Resumed?.Invoke();
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
