using System;
using System.Threading;

namespace DesktopGuy.App.Engine;

/// <summary>
/// Prevents two instances of the *same* character from running at once -
/// an accidental double-launch (double-clicking the exe while a "Start
/// with Windows" copy is already running, or a manual launch racing that)
/// would otherwise leave two overlapping windows and two tray icons for
/// the same character. Different characters can still run side by side -
/// this only guards against the same folder twice, keyed by folder name
/// so each character gets its own independent lock.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex _mutex;
    private bool _owned;

    public SingleInstanceGuard(string characterFolderName)
    {
        _mutex = new Mutex(initiallyOwned: true, $"Local\\DesktopGuy_{characterFolderName}", out bool createdNew);
        _owned = createdNew;
    }

    /// <summary>False if another process already holds the lock for this same character.</summary>
    public bool IsPrimaryInstance => _owned;

    /// <summary>
    /// Releases the lock early, before this process actually exits - lets
    /// a relaunch of the same character (picking a new size, for example,
    /// which restarts into the same folder) acquire it immediately rather
    /// than racing this process's own shutdown.
    /// </summary>
    public void Release()
    {
        if (_owned)
        {
            _mutex.ReleaseMutex();
            _owned = false;
        }
    }

    public void Dispose()
    {
        Release();
        _mutex.Dispose();
    }
}
