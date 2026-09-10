using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices when a terminal-ish app (a shell, Windows Terminal, PuTTY, ...)
/// is running anywhere on the system, so the character can pull up its own
/// little matrix-code terminal. This is a simple process-name heuristic
/// rather than "is a terminal window focused" - good enough to react to
/// "I opened a terminal" without needing window-focus tracking.
/// </summary>
public sealed class TerminalWatcher
{
    private static readonly string[] ProcessNames =
    {
        "cmd", "powershell", "pwsh", "WindowsTerminal", "wt",
        "putty", "kitty", "ConEmu64", "ConEmu", "mintty", "Hyper", "alacritty",
    };

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

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
            bool found = false;
            foreach (var name in ProcessNames)
            {
                var matches = Process.GetProcessesByName(name);
                try
                {
                    if (matches.Length > 0)
                    {
                        found = true;
                    }
                }
                finally
                {
                    foreach (var process in matches)
                    {
                        process.Dispose();
                    }
                }

                if (found)
                {
                    break;
                }
            }

            _isActive = found;
        }
        catch
        {
            _isActive = false;
        }
    }
}
