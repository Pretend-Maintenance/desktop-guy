using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches the system battery (if there is one) via the plain Win32
/// GetSystemPowerStatus call, so a laptop running low on charge (or one
/// that's finished charging) can get a one-off nudge (see
/// CharacterController's battery check). A desktop with no battery reports
/// "unknown" here and this just permanently reads as neither low nor full -
/// no special-casing needed elsewhere for that case.
/// </summary>
public sealed class BatteryWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);
    private const byte LowBatteryPercent = 20;
    private const byte UnknownBatteryPercent = 255;

    private volatile bool _isLow;
    private volatile bool _isFull;

    /// <summary>True when running on battery power and charge is at or below LowBatteryPercent.</summary>
    public bool IsLow => _isLow;

    /// <summary>True when plugged in and charge has reached 100%.</summary>
    public bool IsFull => _isFull;

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
            if (!GetSystemPowerStatus(out var status) || status.BatteryLifePercent == UnknownBatteryPercent)
            {
                _isLow = false;
                _isFull = false;
                return;
            }

            bool onBattery = status.ACLineStatus == 0;
            bool pluggedIn = status.ACLineStatus == 1;
            _isLow = onBattery && status.BatteryLifePercent <= LowBatteryPercent;
            _isFull = pluggedIn && status.BatteryLifePercent >= 100;
        }
        catch
        {
            _isLow = false;
            _isFull = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);
}
