using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Tracks whether the machine currently has a usable network connection,
/// by looking directly at physical wifi/ethernet adapters rather than
/// trusting NetworkInterface.GetIsNetworkAvailable() - that call counts
/// *any* non-loopback interface that's up, which includes virtual
/// adapters (Hyper-V, WSL, Docker, a VPN client, ...) that commonly stay
/// "up" the entire time regardless of whether the machine's actual wifi
/// or ethernet is connected. Filtering to only Wireless80211/Ethernet-type
/// interfaces, and excluding ones whose name/description looks virtual,
/// is what actually reacts to flipping wifi on/off.
///
/// Not a true internet-reachability check (a router with no upstream
/// still reports "available") - cheap and good enough for a mascot's
/// "hey, you're offline" nudge, same trade-off BatteryWatcher makes for
/// battery state.
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

    // Substrings (checked case-insensitively against both Name and
    // Description) that flag an adapter as virtual/not-the-real-connection -
    // these are the common ones that stay "up" regardless of actual wifi/
    // ethernet state and would otherwise mask a real disconnect.
    private static readonly string[] VirtualAdapterMarkers =
    {
        "virtual", "hyper-v", "vmware", "virtualbox", "vbox", "loopback",
        "wsl", "docker", "tap-windows", "tap adapter", "bluetooth",
        "pseudo", "tunnel", "vpn",
    };

    private volatile bool _isAvailable = true;
    private volatile bool _wasWireless;

    /// <summary>True when at least one physical wifi/ethernet adapter is up.</summary>
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
            bool anyPhysicalUp = false;
            bool anyWirelessUp = false;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || !IsPhysicalInterface(nic))
                {
                    continue;
                }

                anyPhysicalUp = true;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    anyWirelessUp = true;
                }
            }

            _isAvailable = anyPhysicalUp;
            if (_isAvailable)
            {
                _wasWireless = anyWirelessUp;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Record("NetworkStatusWatcher.RefreshOnce", ex);
        }
    }

    private static bool IsPhysicalInterface(NetworkInterface nic)
    {
        bool isEthernetOrWifi = nic.NetworkInterfaceType is
            NetworkInterfaceType.Wireless80211 or
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.FastEthernetFx;

        if (!isEthernetOrWifi)
        {
            return false;
        }

        string name = nic.Name ?? string.Empty;
        string description = nic.Description ?? string.Empty;

        foreach (var marker in VirtualAdapterMarkers)
        {
            if (name.Contains(marker, StringComparison.OrdinalIgnoreCase) ||
                description.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
    }
}
