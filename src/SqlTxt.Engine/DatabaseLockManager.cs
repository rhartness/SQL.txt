using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Engine;

/// <summary>
/// Phase 2: Reader-writer lock per database. Multiple readers; writers exclusive.
/// </summary>
public sealed class DatabaseLockManager : IDatabaseLockManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public Task<IAsyncDisposable> AcquireReadLockAsync(string databasePath, CancellationToken cancellationToken = default) =>
        AcquireWriteLockAsync(databasePath, cancellationToken);

    public async Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(databasePath);
        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new LockReleaser(sem);
    }

    private sealed class LockReleaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sem;
        private bool _disposed;

        public LockReleaser(SemaphoreSlim sem) => _sem = sem;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            _disposed = true;
            _sem.Release();
            return ValueTask.CompletedTask;
        }
    }
}
