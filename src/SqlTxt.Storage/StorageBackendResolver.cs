using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Resolves storage backend from database manifest.
/// </summary>
public sealed class StorageBackendResolver : IStorageBackendResolver
{
    private readonly DatabaseCreator _dbCreator;

    public StorageBackendResolver(DatabaseCreator dbCreator)
    {
        _dbCreator = dbCreator;
    }

    public async Task<IStorageBackend> ResolveAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var backend = await _dbCreator.GetStorageBackendAsync(databasePath, cancellationToken).ConfigureAwait(false);
        return StorageBackendFactory.GetStorageBackend(backend);
    }
}
