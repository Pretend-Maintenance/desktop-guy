using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when you're actively pressing keys, anywhere on the system, so
/// the character can react to "I'm typing" rather than only "a terminal
/// happens to be focused". Uses a low-level keyboard hook - the only way
/// to see keystrokes system-wide rather than just within our own window -
/// but it only ever records the *timestamp* of the most recent key-down.
/// It never reads, stores, or reports which keys were pressed; there's no
/// keystroke content anywhere in this class, just "was a key pressed
/// recently".
/// </summary>
public sealed class TypingWatcher : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private static readonly TimeSpan TypingWindow = TimeSpan.FromSeconds(1.5);

    // Kept as a field so the delegate isn't garbage-collected out from
    // under the native hook.
    private readonly HookProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private DateTime _lastKeyDownUtc = DateTime.MinValue;

    public TypingWatcher()
    {
        _hookProc = HookCallback;
    }

    public bool IsTyping => DateTime.UtcNow - _lastKeyDownUtc < TypingWindow;

    /// <summary>
    /// Installs the keyboard hook. Must be called from a thread with a
    /// Windows message loop (the WPF UI thread qualifies). Fails silently
    /// if hook installation isn't permitted on this system - typing
    /// awareness just never activates rather than crashing the app.
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
        catch
        {
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            _lastKeyDownUtc = DateTime.UtcNow;
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
