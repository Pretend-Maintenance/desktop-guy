using System;
using Microsoft.Win32;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when the current user's session locks or unlocks (Win+L, the
/// screensaver kicking in, a domain machine auto-locking, ...) - a
/// different signal from the idle-timeout-based Sleeping/Waking pair,
/// which is just "no input for a while" and can't tell a genuine lock
/// from someone just reading something for a few minutes. Same static-
/// event shape as SystemResumeWatcher; Dispose() unsubscribes for the
/// same reason (SystemEvents holds this as a static reference).
/// </summary>
public sealed class SessionLockWatcher : IDisposable
{
    public event Action? Locked;
    public event Action? Unlocked;

    public SessionLockWatcher()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            Locked?.Invoke();
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            Unlocked?.Invoke();
        }
    }

    public void Dispose()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
