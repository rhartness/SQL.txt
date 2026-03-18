namespace SqlTxt.Contracts;

/// <summary>
/// Resolves the storage backend for a database path.
/// </summary>
public interface IStorageBackendResolver
{
    /// <summary>
    /// Gets the storage backend for the given database path.
    /// </summary>
    Task<IStorageBackend> ResolveAsync(string databasePath, CancellationToken cancellationToken = default);
}
