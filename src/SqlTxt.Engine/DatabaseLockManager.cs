using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Engine;

/// <summary>
/// Per-database schema RW lock plus per-table RW locks. DDL uses exclusive schema lock only.
/// DML/SELECT acquire schema read then ordered table locks (see ADR-009).
/// </summary>
public sealed class DatabaseLockManager : IDatabaseLockManager
{
    private sealed class Hub
    {
        internal readonly RwLockState Schema = new();
        internal readonly ConcurrentDictionary<string, RwLockState> Tables = new(StringComparer.OrdinalIgnoreCase);

        internal RwLockState GetTable(string tableName) =>
            Tables.GetOrAdd(tableName, _ => new RwLockState());
    }

    private readonly ConcurrentDictionary<string, Hub> _hubs = new();

    private static string NormDb(string databasePath) => Path.GetFullPath(databasePath);

    private Hub GetHub(string databasePath) =>
        _hubs.GetOrAdd(NormDb(databasePath), _ => new Hub());

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireReadLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var hub = GetHub(databasePath);
        await hub.Schema.ReadSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        var count = Interlocked.Increment(ref hub.Schema.ReaderCount);
        if (count == 1)
            await hub.Schema.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        hub.Schema.ReadSem.Release();
        return new SchemaReadReleaser(hub.Schema);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var hub = GetHub(databasePath);
        await hub.Schema.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new SchemaWriteReleaser(hub.Schema);
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireTableReadLocksAsync(string databasePath, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default)
    {
        var ordered = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        var acquired = new List<IAsyncDisposable>(ordered.Count);
        try
        {
            foreach (var t in ordered)
                acquired.Add(await AcquireTableReadLockInternalAsync(databasePath, t, cancellationToken).ConfigureAwait(false));
            return new CompositeAsyncDisposable(acquired);
        }
        catch
        {
            await ReleaseAllReverseAsync(acquired).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireTableWriteLocksAsync(string databasePath, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default)
    {
        var ordered = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        var acquired = new List<IAsyncDisposable>(ordered.Count);
        try
        {
            foreach (var t in ordered)
                acquired.Add(await AcquireTableWriteLockInternalAsync(databasePath, t, cancellationToken).ConfigureAwait(false));
            return new CompositeAsyncDisposable(acquired);
        }
        catch
        {
            await ReleaseAllReverseAsync(acquired).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireFkOrderedLocksAsync(
        string databasePath,
        IReadOnlyList<string> sharedTables,
        IReadOnlyList<string> exclusiveTables,
        CancellationToken cancellationToken = default)
    {
        var exclusive = new HashSet<string>(exclusiveTables ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in sharedTables ?? Array.Empty<string>())
            union.Add(t);
        foreach (var t in exclusiveTables ?? Array.Empty<string>())
            union.Add(t);
        var ordered = union.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
        var acquired = new List<IAsyncDisposable>(ordered.Count);
        try
        {
            foreach (var t in ordered)
            {
                if (exclusive.Contains(t))
                    acquired.Add(await AcquireTableWriteLockInternalAsync(databasePath, t, cancellationToken).ConfigureAwait(false));
                else
                    acquired.Add(await AcquireTableReadLockInternalAsync(databasePath, t, cancellationToken).ConfigureAwait(false));
            }
            return new CompositeAsyncDisposable(acquired);
        }
        catch
        {
            await ReleaseAllReverseAsync(acquired).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<IAsyncDisposable> AcquireTableReadLockInternalAsync(string databasePath, string tableName, CancellationToken cancellationToken)
    {
        var hub = GetHub(databasePath);
        var state = hub.GetTable(tableName);
        await state.ReadSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        var count = Interlocked.Increment(ref state.ReaderCount);
        if (count == 1)
            await state.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        state.ReadSem.Release();
        return new TableReadReleaser(state);
    }

    private async Task<IAsyncDisposable> AcquireTableWriteLockInternalAsync(string databasePath, string tableName, CancellationToken cancellationToken)
    {
        var hub = GetHub(databasePath);
        var state = hub.GetTable(tableName);
        await state.WriteSem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new TableWriteReleaser(state);
    }

    private static async Task ReleaseAllReverseAsync(List<IAsyncDisposable> acquired)
    {
        for (var i = acquired.Count - 1; i >= 0; i--)
            await acquired[i].DisposeAsync().ConfigureAwait(false);
    }

    private sealed class RwLockState
    {
        internal readonly SemaphoreSlim ReadSem = new(1, 1);
        internal readonly SemaphoreSlim WriteSem = new(1, 1);
        internal int ReaderCount;
    }

    private sealed class SchemaReadReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal SchemaReadReleaser(RwLockState state) => _state = state;

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

    private sealed class SchemaWriteReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal SchemaWriteReleaser(RwLockState state) => _state = state;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            _disposed = true;
            _state.WriteSem.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TableReadReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal TableReadReleaser(RwLockState state) => _state = state;

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

    private sealed class TableWriteReleaser : IAsyncDisposable
    {
        private readonly RwLockState _state;
        private bool _disposed;

        internal TableWriteReleaser(RwLockState state) => _state = state;

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;
            _disposed = true;
            _state.WriteSem.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CompositeAsyncDisposable : IAsyncDisposable
    {
        private readonly List<IAsyncDisposable> _parts;

        internal CompositeAsyncDisposable(List<IAsyncDisposable> parts) => _parts = parts;

        public async ValueTask DisposeAsync()
        {
            for (var i = _parts.Count - 1; i >= 0; i--)
                await _parts[i].DisposeAsync().ConfigureAwait(false);
        }
    }
}
