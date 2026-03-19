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

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return await _inner.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        await _inner.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
        Save();
    }

    public async Task AppendAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        await _inner.AppendAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
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

    public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        return await _inner.OpenReadStreamAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Stream> OpenWriteStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureLoaded();
        var innerStream = await _inner.OpenWriteStreamAsync(path, cancellationToken).ConfigureAwait(false);
        return new SaveOnDisposeStream(innerStream, Save);
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

    private sealed class SaveOnDisposeStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action _onDispose;
        private bool _disposed;

        public SaveOnDisposeStream(Stream inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                _inner.Dispose();
                _onDispose();
            }
            _disposed = true;
            base.Dispose(disposing);
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
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
