using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Engine;

/// <summary>
/// Phase 2: Reader-writer lock per database. Multiple readers; writers exclusive.
/// </summary>
public sealed class DatabaseLockManager : IDatabaseLockManager
{
    private readonly ConcurrentDictionary<string, RwLockState> _locks = new();

    public async Task<IAsyncDisposable> AcquireReadLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(databasePath);
        var state = _locks.GetOrAdd(key, _ => new RwLockState());

        await state.ReadSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        var count = Interlocked.Increment(ref state.ReaderCount);
        if (count == 1)
            await state.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        state.ReadSem.Release();

        return new ReadLockReleaser(state);
    }

    public async Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(databasePath);
        var state = _locks.GetOrAdd(key, _ => new RwLockState());

        await state.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new WriteLockReleaser(state);
    }

    private sealed class RwLockState
    {
        internal readonly SemaphoreSlim ReadSem = new(1, 1);
        internal readonly SemaphoreSlim WriteSem = new(1, 1);
        internal int ReaderCount;
    }

    private sealed class ReadLockReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal ReadLockReleaser(RwLockState state) => _state = state;

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;

            await _state.ReadSem.WaitAsync().ConfigureAwait(false);
            var count = Interlocked.Decrement(ref _state.ReaderCount);
            if (count == 0)
                _state.WriteSem.Release();
            _state.ReadSem.Release();
        }
    }

    private sealed class WriteLockReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal WriteLockReleaser(RwLockState state) => _state = state;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            _disposed = true;
            _state.WriteSem.Release();
            return ValueTask.CompletedTask;
        }
    }
}
