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
/// (case-normalized, since kernel object names are case-sensitive and a
/// manually-typed `--character` argument could differ in case from the
/// on-disk folder) so each character gets its own independent lock.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? _mutex;
    private bool _owned;

    public SingleInstanceGuard(string characterFolderName)
    {
        string name = $"Local\\DesktopGuy_{characterFolderName.ToLowerInvariant()}";
        try
        {
            _mutex = new Mutex(initiallyOwned: true, name, out bool createdNew);
            _owned = createdNew;
        }
        catch (Exception ex)
        {
            // A named mutex can fail to create for reasons that have nothing
            // to do with a genuine duplicate instance - most realistically,
            // one copy running elevated ("Run as administrator") while
            // another runs normally, which can throw UnauthorizedAccessException
            // when the two try to share the same kernel object across
            // integrity levels. Whatever the cause, a mutex/ACL quirk
            // shouldn't be able to stop the character from launching at all -
            // fail open (assume this is the only instance) rather than
            // crash or silently refuse to start.
            ErrorLog.Record("SingleInstanceGuard", ex);
            _mutex = null;
            _owned = true;
        }
    }

    /// <summary>False if another process already holds the lock for this same character.</summary>
    public bool IsPrimaryInstance => _owned;

    /// <summary>
    /// Releases the lock early, before this process actually exits - lets
    /// a relaunch of the same character (picking a new size, for example,
    /// which restarts into the same folder) acquire it immediately rather
    /// than racing this process's own shutdown. A no-op if construction
    /// fell back to the no-mutex case above.
    /// </summary>
    public void Release()
    {
        if (_owned && _mutex is not null)
        {
            _mutex.ReleaseMutex();
        }

        _owned = false;
    }

    public void Dispose()
    {
        Release();
        _mutex?.Dispose();
    }
}
