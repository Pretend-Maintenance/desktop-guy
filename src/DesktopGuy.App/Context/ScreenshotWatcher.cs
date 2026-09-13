using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when the PrintScreen key is pressed, so the character can react
/// with a little camera-flash startle. Deliberately kept as its own class
/// rather than folded into TypingWatcher: that one's whole point is never
/// knowing which key was pressed, just that some key was - this one only
/// ever checks for exactly one specific key (PrintScreen) and nothing
/// else, so the two classes' privacy scope stays honest and narrow rather
/// than quietly widening TypingWatcher's.
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_SNAPSHOT = 0x2C;

    // Kept as a field so the delegate isn't garbage-collected out from
    // under the native hook.
    private readonly HookProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public event Action? ScreenshotTaken;

    public ScreenshotWatcher()
    {
        _hookProc = HookCallback;
    }

    /// <summary>
    /// Installs the keyboard hook. Must be called from a thread with a
    /// Windows message loop (the WPF UI thread qualifies). Fails silently
    /// if hook installation isn't permitted on this system - screenshot
    /// reactions just never fire rather than crashing the app.
    /// </summary>
    public void Start()
    {
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            IntPtr moduleHandle = GetModuleHandle(currentModule?.ModuleName);
            _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
        }
        catch (Exception ex)
        {
            ErrorLog.Record("ScreenshotWatcher.Start", ex);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (info.vkCode == VK_SNAPSHOT)
            {
                ScreenshotTaken?.Invoke();
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
