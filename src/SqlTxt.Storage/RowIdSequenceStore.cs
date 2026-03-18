using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Persists and allocates _RowId sequence per table in ~System/rowid.
/// </summary>
public sealed class RowIdSequenceStore : IRowIdSequenceStore
{
    private const string RowIdDir = "rowid";

    private readonly Contracts.IFileSystemAccessor _fs;

    public RowIdSequenceStore(Contracts.IFileSystemAccessor fs) => _fs = fs;

    public async Task<long> GetNextAndIncrementAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var path = _fs.Combine(databasePath, "~System", RowIdDir, tableName + ".txt");
        var dir = Path.GetDirectoryName(path)!;
        _fs.CreateDirectory(dir);

        long next;
        if (_fs.FileExists(path))
        {
            var content = await _fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            next = long.TryParse(content.Trim(), out var v) ? v : 0;
        }
        else
        {
            next = 0;
        }

        var newValue = next + 1;
        await _fs.WriteAllTextAsync(path, newValue.ToString(), cancellationToken).ConfigureAwait(false);
        return next;
    }

    public async Task<(long Start, int Count)> GetNextRangeAndIncrementAsync(string databasePath, string tableName, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return (0, 0);

        var path = _fs.Combine(databasePath, "~System", RowIdDir, tableName + ".txt");
        var dir = Path.GetDirectoryName(path)!;
        _fs.CreateDirectory(dir);

        long next;
        if (_fs.FileExists(path))
        {
            var content = await _fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            next = long.TryParse(content.Trim(), out var v) ? v : 0;
        }
        else
        {
            next = 0;
        }

        var newValue = next + count;
        await _fs.WriteAllTextAsync(path, newValue.ToString(), cancellationToken).ConfigureAwait(false);
        return (next, count);
    }
}
