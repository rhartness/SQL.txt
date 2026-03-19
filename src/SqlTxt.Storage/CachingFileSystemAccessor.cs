using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Wraps IFileSystemAccessor with an LRU cache of file contents.
/// Keeps hot shards, indexes, and schema files in memory to reduce disk I/O.
/// Write-through: all writes go to disk and update or invalidate the cache.
/// </summary>
public sealed class CachingFileSystemAccessor : IFileSystemAccessor
{
    private readonly IFileSystemAccessor _inner;
    private readonly long _maxCachedBytes;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lruOrder = new();
    private long _totalCachedBytes;
    private readonly object _cacheLock = new();

    private sealed class CacheEntry
    {
        public byte[] Content;
        public long Size => Content.Length;

        public CacheEntry(byte[] content) => Content = content;
    }

    /// <summary>
    /// Creates a caching file system accessor.
    /// </summary>
    /// <param name="inner">The underlying file system accessor.</param>
    /// <param name="maxCachedBytes">Maximum bytes to cache (default 64 MB). Eviction is LRU.</param>
    public CachingFileSystemAccessor(IFileSystemAccessor inner, long maxCachedBytes = 67_108_864)
    {
        _inner = inner;
        _maxCachedBytes = Math.Max(0, maxCachedBytes);
    }

    public void CreateDirectory(string path) => _inner.CreateDirectory(path);

    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

    public bool FileExists(string path) => _inner.FileExists(path);

    public string GetFullPath(string path) => _inner.GetFullPath(path);

    public string Combine(params string[] paths) => _inner.Combine(paths);

    public IReadOnlyList<string> GetDirectories(string path) => _inner.GetDirectories(path);

    public IReadOnlyList<string> GetFiles(string path) => _inner.GetFiles(path);

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await GetOrLoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return bytes is not null ? Encoding.UTF8.GetString(bytes) : await _inner.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        var bytes = Encoding.UTF8.GetBytes(content);
        SetCache(path, bytes);
    }

    public async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        // True append on disk — avoid O(n²) rewrite when the file grows (Phase 3.5).
        await _inner.AppendAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        if (content.Length == 0)
            return;
        var appended = Encoding.UTF8.GetBytes(content);
        AppendBytesToCachedFile(path, appended);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        var cached = await GetOrLoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return cached ?? await _inner.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        await _inner.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
        SetCache(path, content);
    }

    public async Task AppendAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        await _inner.AppendAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
        if (content.Length == 0)
            return;
        AppendBytesToCachedFile(path, content);
    }

    public async Task<IReadOnlyList<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await GetOrLoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes is not null)
        {
            var text = Encoding.UTF8.GetString(bytes);
            return text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        }
        return await _inner.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bytes = await GetOrLoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes is not null)
        {
            var text = Encoding.UTF8.GetString(bytes);
            var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
                await Task.Yield();
            }
            yield break;
        }
        await foreach (var line in _inner.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
            yield return line;
    }

    public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await GetOrLoadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes is not null)
            return new MemoryStream(bytes, writable: false);
        return await _inner.OpenReadStreamAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public Task<Stream> OpenWriteStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        InvalidateCache(path);
        return _inner.OpenWriteStreamAsync(path, cancellationToken);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        InvalidateCache(sourcePath);
        InvalidateCache(destinationPath);
        _inner.MoveFile(sourcePath, destinationPath);
    }

    public void DeleteFile(string path)
    {
        InvalidateCache(path);
        _inner.DeleteFile(path);
    }

    public long GetFileLength(string path)
    {
        var key = NormalizeKey(path);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                TouchLru(key);
                return entry.Size;
            }
        }
        return _inner.GetFileLength(path);
    }

    public void DeleteDirectory(string path)
    {
        InvalidateCacheUnder(path);
        _inner.DeleteDirectory(path);
    }

    private static string NormalizeKey(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var full = Path.GetFullPath(path);
        return full.Replace('\\', '/');
    }

    private async Task<byte[]?> GetOrLoadBytesAsync(string path, CancellationToken cancellationToken)
    {
        var key = NormalizeKey(path);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                TouchLru(key);
                return entry.Content;
            }
        }

        if (!_inner.FileExists(path))
            return null;

        var bytes = await _inner.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        lock (_cacheLock)
        {
            AddToCache(key, bytes);
        }
        return bytes;
    }

    private void SetCache(string path, byte[] content)
    {
        var key = NormalizeKey(path);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var old))
            {
                _totalCachedBytes -= old.Size;
                _lruOrder.Remove(key);
            }
            AddToCache(key, content);
        }
    }

    /// <summary>
    /// Extends cached content when this path is already in the LRU cache; no disk read.
    /// </summary>
    private void AppendBytesToCachedFile(string path, byte[] appended)
    {
        if (appended.Length == 0)
            return;
        var key = NormalizeKey(path);
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(key, out var entry))
                return;
            _totalCachedBytes -= entry.Size;
            _lruOrder.Remove(key);
            _cache.Remove(key);
            var combined = new byte[entry.Content.Length + appended.Length];
            Buffer.BlockCopy(entry.Content, 0, combined, 0, entry.Content.Length);
            Buffer.BlockCopy(appended, 0, combined, entry.Content.Length, appended.Length);
            AddToCache(key, combined);
        }
    }

    private void AddToCache(string key, byte[] content)
    {
        if (_maxCachedBytes <= 0 || content.Length > _maxCachedBytes)
            return;

        while (_totalCachedBytes + content.Length > _maxCachedBytes && _lruOrder.First is { } first)
        {
            var evictKey = first.Value;
            _lruOrder.RemoveFirst();
            if (_cache.Remove(evictKey, out var evicted))
                _totalCachedBytes -= evicted.Size;
        }

        _cache[key] = new CacheEntry(content);
        _lruOrder.AddLast(key);
        _totalCachedBytes += content.Length;
    }

    private void TouchLru(string key)
    {
        if (_lruOrder.Remove(key))
            _lruOrder.AddLast(key);
    }

    private void InvalidateCache(string path)
    {
        var key = NormalizeKey(path);
        lock (_cacheLock)
        {
            if (_cache.Remove(key, out var entry))
            {
                _totalCachedBytes -= entry.Size;
                _lruOrder.Remove(key);
            }
        }
    }

    private void InvalidateCacheUnder(string path)
    {
        var norm = NormalizeKey(path);
        var prefix = string.IsNullOrEmpty(norm) ? "" : norm.TrimEnd('/') + "/";
        lock (_cacheLock)
        {
            var toRemove = _cache.Keys.Where(k =>
                (!string.IsNullOrEmpty(prefix) && k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
                k.Equals(norm, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in toRemove)
            {
                if (_cache.Remove(key, out var entry))
                {
                    _totalCachedBytes -= entry.Size;
                    _lruOrder.Remove(key);
                }
            }
        }
    }
}
