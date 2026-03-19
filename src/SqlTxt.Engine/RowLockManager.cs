using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Engine;

/// <summary>
/// Per (database, table, rowId) exclusive locks; useful when table-level lock allows concurrent writers.
/// </summary>
public sealed class RowLockManager : IRowLockManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sems = new();

    private static string Key(string databasePath, string tableName, long rowId) =>
        Path.GetFullPath(databasePath) + "|" + tableName + "|" + rowId;

    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireRowExclusiveAsync(
        string databasePath,
        string tableName,
        long rowId,
        CancellationToken cancellationToken = default)
    {
        var sem = _sems.GetOrAdd(Key(databasePath, tableName, rowId), _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(sem);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sem;
        private bool _disposed;

        internal Releaser(SemaphoreSlim sem) => _sem = sem;

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
