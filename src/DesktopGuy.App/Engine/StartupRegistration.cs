using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Registers (or unregisters) the app to launch automatically when you log
/// into Windows, via the standard per-user "Run" registry key - the same
/// mechanism most tray apps use. This is HKCU (current user), not HKLM, so
/// it never needs admin rights and only affects the signed-in user.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DesktopGuy";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>
    /// Points the Run entry at whichever exe is currently running, with
    /// --character pinned explicitly so it doesn't drift if other
    /// character folders get added later.
    /// </summary>
    public static void SetEnabled(bool enabled, string characterId)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("Couldn't determine the running executable's path.");
        }

        key.SetValue(ValueName, $"\"{exePath}\" --character {characterId}");
    }
}
