using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Tracks whether the machine currently has a usable network connection,
/// via the same best-effort signal Windows itself exposes
/// (NetworkInterface.GetIsNetworkAvailable() - at least one non-loopback
/// interface that's up). Not a true internet-reachability check (a router
/// with no upstream still reports "available"), but cheap, synchronous,
/// and good enough for a mascot's "hey, you're offline" nudge - same
/// trade-off BatteryWatcher makes for battery state.
///
/// Reacts immediately to NetworkChange's events, with a slow poll running
/// alongside as a safety net in case an event gets missed (some adapters/
/// drivers are unreliable about raising them). Also remembers whether the
/// connection looked wireless the last time it was up, so a phrase can say
/// "wifi" specifically instead of a generic "network" even at the moment
/// it just dropped (by then there's no "active" interface left to inspect).
/// </summary>
public sealed class NetworkStatusWatcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(3);

    private volatile bool _isAvailable = true;
    private volatile bool _wasWireless;

    /// <summary>True when at least one non-loopback network interface is up.</summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>Best-effort guess at whether the (most recent) connection was over wifi rather than wired.</summary>
    public bool IsWireless => _wasWireless;

    public NetworkStatusWatcher()
    {
        RefreshOnce();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    public void Start(CancellationToken cancellationToken)
    {
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

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => RefreshOnce();

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => RefreshOnce();

    private void RefreshOnce()
    {
        try
        {
            _isAvailable = NetworkInterface.GetIsNetworkAvailable();
            if (_isAvailable)
            {
                _wasWireless = HasActiveWirelessInterface();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Record("NetworkStatusWatcher.RefreshOnce", ex);
        }
    }

    private static bool HasActiveWirelessInterface()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }
}
