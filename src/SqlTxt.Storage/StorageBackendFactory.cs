using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Factory for resolving IStorageBackend from manifest storageBackend value.
/// </summary>
public static class StorageBackendFactory
{
    /// <summary>
    /// Returns the storage backend for the given manifest value.
    /// </summary>
    /// <param name="storageBackend">Value from manifest ("text" or "binary").</param>
    /// <returns>TextStorageBackend or BinaryStorageBackend. Defaults to text for null/invalid.</returns>
    public static IStorageBackend GetStorageBackend(string? storageBackend)
    {
        var normalized = storageBackend?.Trim().ToLowerInvariant();
        return normalized == "binary" ? new BinaryStorageBackend() : new TextStorageBackend();
    }
}
