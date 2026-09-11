using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopGuy.App.Engine;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when a terminal-ish app (a shell, Windows Terminal, PuTTY, ...)
/// is the window you're actually focused on right now, so the character
/// can pull up its own little matrix-code terminal. Checked against the
/// foreground window rather than "is such a process running anywhere" -
/// otherwise launching the app from a terminal (like `dotnet run`) would
/// leave it stuck showing "hacking" forever, since that terminal stays
/// open in the background for the whole session.
/// </summary>
public sealed class TerminalWatcher
{
    private static readonly string[] ProcessNames =
    {
        "cmd", "powershell", "pwsh", "WindowsTerminal", "wt",
        "putty", "kitty", "ConEmu64", "ConEmu", "mintty", "Hyper", "alacritty",
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);

    private volatile bool _isActive;

    public bool IsActive => _isActive;

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
            int? processId = Win32Interop.GetForegroundProcessId();
            if (processId is null)
            {
                _isActive = false;
                return;
            }

            using var process = Process.GetProcessById(processId.Value);
            _isActive = ProcessNames.Any(name => string.Equals(name, process.ProcessName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // The foreground process can disappear between the lookup and
            // reading it (window closed mid-poll); just wait for the next tick.
            _isActive = false;
        }
    }
}
