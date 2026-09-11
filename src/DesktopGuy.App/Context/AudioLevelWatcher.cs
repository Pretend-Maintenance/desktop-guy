using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopGuy.App.Context;

/// <summary>
/// Watches the system's actual audio output level (via the Windows Core
/// Audio "peak meter" API on the default playback device), so
/// <see cref="MediaContextWatcher"/> can tell the difference between
/// "something reports itself as playing" and "something is actually
/// audible." A muted YouTube tab or muted speakers still reports
/// PlaybackStatus.Playing through the media-session API - this catches
/// both, since either one means nothing is really coming out of the
/// speakers, without needing to know anything about the specific app.
///
/// This is plain COM interop (mmdeviceapi.h / endpointvolume.h), not a
/// WinRT API - only the one method actually needed (GetPeakValue) is
/// declared on each interface, which is enough for COM's vtable-based
/// dispatch as long as any methods that come before it in the real
/// interface are also declared in order (they are, here). If any of this
/// fails for any reason (missing audio device, locked-down system), it
/// degrades to always reporting audible, so it can never make the
/// character seem stuck rather than just missing this one refinement.
/// </summary>
public sealed class AudioLevelWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(300);

    // A brief dip to silence (e.g. a quiet beat) shouldn't immediately read
    // as "muted" - only treat it as silent once it's stayed that way for a
    // little while.
    private static readonly TimeSpan SilenceHoldDuration = TimeSpan.FromSeconds(1.5);
    private const float SilenceThreshold = 0.02f;

    private IAudioMeterInformation? _meter;
    private DateTime _lastAudibleUtc = DateTime.UtcNow;

    /// <summary>True until the output has read silent for SilenceHoldDuration straight.</summary>
    public bool IsAudible { get; private set; } = true;

    public void Start(CancellationToken cancellationToken)
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(/* eRender */ 0, /* eMultimedia */ 1, out var device);

            Guid iid = typeof(IAudioMeterInformation).GUID;
            device.Activate(ref iid, /* CLSCTX_ALL */ 23, IntPtr.Zero, out object meterObj);
            _meter = (IAudioMeterInformation)meterObj;
        }
        catch
        {
            _meter = null;
            return;
        }

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
        if (_meter is null)
        {
            return;
        }

        try
        {
            _meter.GetPeakValue(out float peak);
            if (peak > SilenceThreshold)
            {
                _lastAudibleUtc = DateTime.UtcNow;
            }

            IsAudible = DateTime.UtcNow - _lastAudibleUtc < SilenceHoldDuration;
        }
        catch
        {
            // The default device can change out from under us (headphones
            // unplugged, etc.) - just keep the last known state rather
            // than guessing until the next successful read.
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        // Declared (but never called) purely to keep GetDefaultAudioEndpoint
        // at the correct vtable slot - it's the real interface's 1st method.
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        int GetPeakValue(out float pfPeak);
    }
}
