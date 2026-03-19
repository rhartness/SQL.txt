using System.Collections.Concurrent;
using System.Globalization;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Persists committed and next-allocation xid in ~System/mvcc/xid.state (two lines).
/// </summary>
public sealed class MvccXidStore : IMvccXidStore
{
    private readonly IFileSystemAccessor _fs;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public MvccXidStore(IFileSystemAccessor fs) => _fs = fs;

    private static string Norm(string databasePath) => Path.GetFullPath(databasePath);

    private string StatePath(string databasePath) =>
        _fs.Combine(databasePath, "~System", "mvcc", "xid.state");

    private async Task<ReleaseSem> EnterDbAsync(string databasePath, CancellationToken cancellationToken)
    {
        var sem = _locks.GetOrAdd(Norm(databasePath), _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ReleaseSem(sem);
    }

    private async Task<(long Committed, long Next)> ReadStateAsync(string databasePath, CancellationToken cancellationToken)
    {
        var path = StatePath(databasePath);
        if (!_fs.FileExists(path))
            return (0L, 1L);
        var text = await _fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        long c = 0, n = 1;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 0 && long.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v0))
            c = v0;
        if (lines.Length > 1 && long.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v1))
            n = v1;
        return (c, Math.Max(n, c + 1));
    }

    private async Task WriteStateAsync(string databasePath, long committed, long next, CancellationToken cancellationToken)
    {
        var path = StatePath(databasePath);
        var dir = Path.GetDirectoryName(path)!;
        _fs.CreateDirectory(dir);
        var text = committed.ToString(CultureInfo.InvariantCulture) + Environment.NewLine + next.ToString(CultureInfo.InvariantCulture);
        await _fs.WriteAllTextAsync(path, text, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetCommittedXidAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        await using (var gate = await EnterDbAsync(databasePath, cancellationToken).ConfigureAwait(false))
        {
            var (c, _) = await ReadStateAsync(databasePath, cancellationToken).ConfigureAwait(false);
            return c;
        }
    }

    /// <inheritdoc />
    public async Task<long> AllocateXidAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        await using (var gate = await EnterDbAsync(databasePath, cancellationToken).ConfigureAwait(false))
        {
            var (c, n) = await ReadStateAsync(databasePath, cancellationToken).ConfigureAwait(false);
            var xid = n;
            await WriteStateAsync(databasePath, c, n + 1, cancellationToken).ConfigureAwait(false);
            return xid;
        }
    }

    /// <inheritdoc />
    public async Task CommitXidAsync(string databasePath, long xid, CancellationToken cancellationToken = default)
    {
        await using (var gate = await EnterDbAsync(databasePath, cancellationToken).ConfigureAwait(false))
        {
            var (c, n) = await ReadStateAsync(databasePath, cancellationToken).ConfigureAwait(false);
            var newC = Math.Max(c, xid);
            await WriteStateAsync(databasePath, newC, n, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class ReleaseSem : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sem;
        internal ReleaseSem(SemaphoreSlim sem) => _sem = sem;
        public ValueTask DisposeAsync()
        {
            _sem.Release();
            return ValueTask.CompletedTask;
        }
    }
}
