using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Caches storage backend resolution per database path to avoid repeated manifest reads.
/// </summary>
public sealed class CachingStorageBackendResolver : IStorageBackendResolver
{
    private readonly IStorageBackendResolver _inner;
    private readonly ConcurrentDictionary<string, IStorageBackend> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CachingStorageBackendResolver(IStorageBackendResolver inner)
    {
        _inner = inner;
    }

    public async Task<IStorageBackend> ResolveAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFullPath(databasePath);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var backend = await _inner.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false);
        _cache.TryAdd(key, backend);
        return backend;
    }
}
