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

    /// <summary>
    /// Wraps the whole diff-and-notify step in a try/catch, not just the
    /// enumeration below - the same whole-body shape UsbDriveWatcher's
    /// RefreshOnce uses. Without this, an exception anywhere in here
    /// (including, in principle, a subscriber's own event handler throwing)
    /// would escape into PollLoopAsync's fire-and-forget task and silently
    /// kill the polling loop forever, with nothing logged - not just fail
    /// to report a device this one time.
    /// </summary>
    private async Task RefreshOnceAsync()
    {
        try
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
        catch (Exception ex)
        {
            ErrorLog.Record("BluetoothWatcher.RefreshOnceAsync", ex);
        }
    }

    private static async Task<HashSet<string>> GetConnectedDeviceNamesAsync()
    {
        var result = new HashSet<string>();

        // Each API queried independently, with its own try/catch - classic
        // and BLE are separate stacks under the hood, and a machine with
        // one misbehaving (or a radio that only supports one) shouldn't
        // lose the other's results too.
        await TryAddConnectedNamesAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true), result);
        await TryAddConnectedNamesAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), result);

        return result;
    }

    private static async Task TryAddConnectedNamesAsync(string selector, HashSet<string> result)
    {
        try
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
        catch (Exception ex)
        {
            ErrorLog.Record("BluetoothWatcher.TryAddConnectedNamesAsync", ex);
        }
    }
}
