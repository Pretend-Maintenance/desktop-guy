using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches Windows' own notification feed (the same list that backs the
/// Action Center) for Discord's toast notifications, so the character can
/// react to an incoming call or a new message without talking to Discord
/// directly - there's no public API for that, but Windows already knows
/// about every toast any app raises.
///
/// This requires the user to grant "notification access" the first time
/// the app runs (a standard Windows permission prompt, the same one apps
/// like Cortana or Phone Link ask for). If access is denied, unavailable,
/// or this Windows version doesn't support it for unpackaged apps, this
/// watcher quietly does nothing rather than crashing the app - the rest
/// of the character (idle/sleep, wandering, media awareness) is
/// unaffected either way.
/// </summary>
public sealed class NotificationWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private UserNotificationListener? _listener;
    private HashSet<uint> _knownNotificationIds = new();

    public event Action<DiscordEvent>? DiscordEventDetected;

    /// <summary>True once access was requested and granted.</summary>
    public bool IsActive { get; private set; }

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            if (status != UserNotificationListenerAccessStatus.Allowed)
            {
                _listener = null;
                return false;
            }
        }
        catch
        {
            // API unsupported on this system/build (e.g. requires package
            // identity here) - fall back to no Discord awareness.
            _listener = null;
            return false;
        }

        IsActive = true;
        _ = PollLoopAsync(cancellationToken);
        return true;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                var notifications = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                var currentIds = notifications.Select(n => n.Id).ToHashSet();

                foreach (var notification in notifications)
                {
                    if (!_knownNotificationIds.Contains(notification.Id))
                    {
                        Inspect(notification);
                    }
                }

                _knownNotificationIds = currentIds;
            }
            catch
            {
                // Transient failure reading the feed; just try again next tick.
            }

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

    private void Inspect(UserNotification notification)
    {
        string appName = notification.AppInfo?.DisplayInfo?.DisplayName ?? "";
        if (!appName.Contains("Discord", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string text = ExtractText(notification);
        bool looksLikeCall =
            text.Contains("calling", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("incoming call", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("video call", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("voice call", StringComparison.OrdinalIgnoreCase);

        DiscordEventDetected?.Invoke(looksLikeCall ? DiscordEvent.IncomingCall : DiscordEvent.NewMessage);
    }

    private static string ExtractText(UserNotification notification)
    {
        try
        {
            var binding = notification.Notification.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding is null)
            {
                return "";
            }

            return string.Join(" ", binding.GetTextElements().Select(t => t.Text));
        }
        catch
        {
            return "";
        }
    }
}
