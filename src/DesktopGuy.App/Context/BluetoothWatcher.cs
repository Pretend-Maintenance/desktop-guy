using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches paired Bluetooth devices (classic and BLE - headphones,
/// speakers, a mouse/keyboard, ...) for connecting/disconnecting, and
/// reports which one by name. Polled the same way UsbDriveWatcher polls
/// removable drives - a plain diff of "which devices are connected now"
/// against last poll - rather than subscribing to DeviceWatcher's
/// per-device update events, since that needs one watcher instance kept
/// alive and correlated per device; a simple periodic re-query of "who's
/// connected right now" is easier to reason about for something this
/// low-stakes, at the cost of a few seconds of latency.
///
/// This is a WinRT API, like MediaContextWatcher - works from an
/// unpackaged Win32 app, but degrades quietly (just reports nothing) if
/// it's ever unavailable rather than crashing the app.
/// </summary>
public sealed class BluetoothWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly string[] IsConnectedProperty = { "System.Devices.Aep.IsConnected" };

    private HashSet<string> _lastConnectedNames = new();

    public event Action<string>? DeviceConnected;
    public event Action<string>? DeviceDisconnected;

    public void Start(CancellationToken cancellationToken)
    {
        _ = PollLoopAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        // First read establishes the baseline silently - otherwise every
        // device already connected when the app starts would fire a
        // "connected" bubble immediately.
        _lastConnectedNames = await GetConnectedDeviceNamesAsync();

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

            await RefreshOnceAsync();
        }
    }

    private async Task RefreshOnceAsync()
    {
        var current = await GetConnectedDeviceNamesAsync();

        foreach (var name in current)
        {
            if (!_lastConnectedNames.Contains(name))
            {
                DeviceConnected?.Invoke(name);
            }
        }

        foreach (var name in _lastConnectedNames)
        {
            if (!current.Contains(name))
            {
                DeviceDisconnected?.Invoke(name);
            }
        }

        _lastConnectedNames = current;
    }

    private static async Task<HashSet<string>> GetConnectedDeviceNamesAsync()
    {
        var result = new HashSet<string>();

        try
        {
            await AddConnectedNamesAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), result);
            await AddConnectedNamesAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), result);
        }
        catch (Exception ex)
        {
            ErrorLog.Record("BluetoothWatcher.GetConnectedDeviceNamesAsync", ex);
        }

        return result;
    }

    private static async Task AddConnectedNamesAsync(string selector, HashSet<string> result)
    {
        var devices = await DeviceInformation.FindAllAsync(selector, IsConnectedProperty);
        foreach (var device in devices)
        {
            if (device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value) &&
                value is bool isConnected && isConnected)
            {
                result.Add(string.IsNullOrWhiteSpace(device.Name) ? "a device" : device.Name);
            }
        }
    }
}
