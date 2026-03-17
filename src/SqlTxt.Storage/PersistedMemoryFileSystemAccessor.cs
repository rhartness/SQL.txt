using System.Text.Json;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Wraps MemoryFileSystemAccessor with persistence to a JSON file.
/// Loads on first access; saves after each write operation.
/// </summary>
public sealed class PersistedMemoryFileSystemAccessor : IFileSystemAccessor
{
    private readonly MemoryFileSystemAccessor _inner;
    private readonly string _persistencePath;
    private bool _loaded;

    public PersistedMemoryFileSystemAccessor(string persistencePath)
    {
        _persistencePath = Path.GetFullPath(persistencePath);
        _inner = new MemoryFileSystemAccessor();
    }

    public void CreateDirectory(string path)
    {
        EnsureLoaded();
        _inner.CreateDirectory(path);
        Save();
    }

    public bool DirectoryExists(string path)
    {
        EnsureLoaded();
        return _inner.DirectoryExists(path);
    }

    public bool FileExists(string path)
    {
        EnsureLoaded();
        return _inner.FileExists(path);
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return await _inner.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        await _inner.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        Save();
    }

    public async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        await _inner.AppendAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        Save();
    }

    public async Task<IReadOnlyList<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return await _inner.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return _inner.ReadLinesAsync(path, cancellationToken);
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        EnsureLoaded();
        _inner.MoveFile(sourcePath, destinationPath);
        Save();
    }

    public void DeleteFile(string path)
    {
        EnsureLoaded();
        _inner.DeleteFile(path);
        Save();
    }

    public string GetFullPath(string path) => _inner.GetFullPath(path);

    public string Combine(params string[] paths) => _inner.Combine(paths);

    public IReadOnlyList<string> GetDirectories(string path)
    {
        EnsureLoaded();
        return _inner.GetDirectories(path);
    }

    public IReadOnlyList<string> GetFiles(string path)
    {
        EnsureLoaded();
        return _inner.GetFiles(path);
    }

    public long GetFileLength(string path)
    {
        EnsureLoaded();
        return _inner.GetFileLength(path);
    }

    public void DeleteDirectory(string path)
    {
        EnsureLoaded();
        _inner.DeleteDirectory(path);
        Save();
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        if (File.Exists(_persistencePath))
        {
            var json = File.ReadAllText(_persistencePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null && dict.Count > 0)
            {
                var normalized = dict.ToDictionary(
                    kv => MemoryFileSystemAccessor.NormalizeKey(kv.Key),
                    kv => kv.Value);
                _inner.LoadFrom(normalized);
            }
        }
    }

    private void Save()
    {
        var dict = _inner.GetAllFiles();
        var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = false });
        var dir = Path.GetDirectoryName(_persistencePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_persistencePath, json);
    }

    /// <summary>
    /// Gets the virtual database root name derived from the persistence file (e.g., "WikiDb" from "WikiDb.wasmdb").
    /// </summary>
    public static string GetVirtualRootFromPersistencePath(string persistencePath)
    {
        var fileName = Path.GetFileName(persistencePath);
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
