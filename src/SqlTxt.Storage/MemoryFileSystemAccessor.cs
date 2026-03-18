using System.Runtime.CompilerServices;

namespace SqlTxt.Storage;

/// <summary>
/// In-memory implementation of IFileSystemAccessor for WASM-compatible storage.
/// Uses normalized path keys with "/" separator for cross-platform consistency.
/// </summary>
public sealed class MemoryFileSystemAccessor : Contracts.IFileSystemAccessor
{
    private readonly Dictionary<string, string> _files = new();
    private readonly Dictionary<string, byte[]> _binaryFiles = new();
    private readonly HashSet<string> _directories = new();

    internal static string NormalizeKey(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != ".").ToArray();
        return string.Join("/", parts);
    }

    private static string Normalize(string path) => NormalizeKey(path);

    public void CreateDirectory(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm))
            return;

        var current = "";
        foreach (var part in norm.Split('/'))
        {
            current = string.IsNullOrEmpty(current) ? part : current + "/" + part;
            _directories.Add(current);
        }
    }

    public bool DirectoryExists(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm))
            return true;
        return _directories.Contains(norm) ||
               _files.Keys.Any(k => k.StartsWith(norm + "/", StringComparison.Ordinal));
    }

    public bool FileExists(string path)
    {
        var norm = Normalize(path);
        return _files.ContainsKey(norm) || _binaryFiles.ContainsKey(norm);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        if (_binaryFiles.TryGetValue(norm, out var bytes))
            return Task.FromResult(bytes);
        if (_files.TryGetValue(norm, out var content))
            return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(content));
        throw new FileNotFoundException("File not found", path);
    }

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        EnsureParentDirectories(norm);
        _binaryFiles[norm] = content;
        _files.Remove(norm);
        return Task.CompletedTask;
    }

    public async Task AppendAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        var existing = await ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var combined = new byte[existing.Length + content.Length];
        Buffer.BlockCopy(existing, 0, combined, 0, existing.Length);
        Buffer.BlockCopy(content, 0, combined, existing.Length, content.Length);
        await WriteAllBytesAsync(path, combined, cancellationToken).ConfigureAwait(false);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        if (!_files.TryGetValue(norm, out var content))
            throw new FileNotFoundException("File not found", path);
        return Task.FromResult(content);
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        EnsureParentDirectories(norm);
        _files[norm] = content;
        _binaryFiles.Remove(norm);
        return Task.CompletedTask;
    }

    public async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        var existing = _files.TryGetValue(norm, out var current) ? current : "";
        await WriteAllTextAsync(path, existing + content, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        string content;
        if (_files.TryGetValue(norm, out var s))
            content = s;
        else if (_binaryFiles.TryGetValue(norm, out var bytes))
            content = System.Text.Encoding.UTF8.GetString(bytes);
        else
            throw new FileNotFoundException("File not found", path);
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        return Task.FromResult<IReadOnlyList<string>>(lines);
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var norm = Normalize(path);
        string content;
        if (_files.TryGetValue(norm, out var s))
            content = s;
        else if (_binaryFiles.TryGetValue(norm, out var bytes))
            content = System.Text.Encoding.UTF8.GetString(bytes);
        else
            throw new FileNotFoundException("File not found", path);
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
            await Task.Yield();
        }
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);
        if (_files.TryGetValue(srcNorm, out var content))
        {
            EnsureParentDirectories(destNorm);
            _files[destNorm] = content;
            _files.Remove(srcNorm);
        }
        else if (_binaryFiles.TryGetValue(srcNorm, out var bytes))
        {
            EnsureParentDirectories(destNorm);
            _binaryFiles[destNorm] = bytes;
            _binaryFiles.Remove(srcNorm);
        }
        else
            throw new FileNotFoundException("File not found", sourcePath);
    }

    public void DeleteFile(string path)
    {
        var norm = Normalize(path);
        _files.Remove(norm);
        _binaryFiles.Remove(norm);
    }

    public string GetFullPath(string path)
    {
        var norm = Normalize(path);
        return string.IsNullOrEmpty(norm) ? "/" : "/" + norm;
    }

    public string Combine(params string[] paths)
    {
        var joined = string.Join("/", paths.Select(p => p?.Replace('\\', '/').Trim('/') ?? ""));
        return Normalize(joined);
    }

    public IReadOnlyList<string> GetDirectories(string path)
    {
        var norm = Normalize(path);
        var prefix = string.IsNullOrEmpty(norm) ? "" : norm + "/";
        var seen = new HashSet<string>();
        foreach (var dir in _directories)
        {
            if (dir.StartsWith(prefix, StringComparison.Ordinal))
            {
                var remainder = dir[prefix.Length..];
                var firstSegment = remainder.Contains('/') ? remainder[..remainder.IndexOf('/')] : remainder;
                if (!string.IsNullOrEmpty(firstSegment))
                    seen.Add(prefix + firstSegment);
            }
        }
        foreach (var file in _files.Keys)
        {
            if (file.StartsWith(prefix, StringComparison.Ordinal))
            {
                var remainder = file[prefix.Length..];
                var firstSegment = remainder.Contains('/') ? remainder[..remainder.IndexOf('/')] : remainder;
                if (!string.IsNullOrEmpty(firstSegment))
                    seen.Add(prefix.TrimEnd('/') + "/" + firstSegment);
            }
        }
        return seen.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> GetFiles(string path)
    {
        var norm = Normalize(path);
        var prefix = string.IsNullOrEmpty(norm) ? "" : norm + "/";
        var result = new List<string>();
        foreach (var file in _files.Keys)
        {
            if (file.StartsWith(prefix, StringComparison.Ordinal))
            {
                var remainder = file[prefix.Length..];
                if (!remainder.Contains('/'))
                    result.Add(prefix + remainder);
            }
        }
        return result.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public long GetFileLength(string path)
    {
        var norm = Normalize(path);
        if (_files.TryGetValue(norm, out var content))
            return content.Length;
        if (_binaryFiles.TryGetValue(norm, out var bytes))
            return bytes.Length;
        throw new FileNotFoundException("File not found", path);
    }

    public void DeleteDirectory(string path)
    {
        var norm = Normalize(path);
        var prefix = string.IsNullOrEmpty(norm) ? "" : norm + "/";
        var toRemove = _files.Keys.Where(k => k == norm || k.StartsWith(prefix)).ToList();
        foreach (var k in toRemove)
            _files.Remove(k);
        var binaryToRemove = _binaryFiles.Keys.Where(k => k == norm || k.StartsWith(prefix)).ToList();
        foreach (var k in binaryToRemove)
            _binaryFiles.Remove(k);
        var dirsToRemove = _directories.Where(d => d == norm || d.StartsWith(prefix)).ToList();
        foreach (var d in dirsToRemove)
            _directories.Remove(d);
    }

    private void EnsureParentDirectories(string path)
    {
        var idx = path.LastIndexOf('/');
        if (idx > 0)
            CreateDirectory(path[..idx]);
    }

    /// <summary>
    /// Returns the raw file contents for serialization (e.g., persistence).
    /// </summary>
    internal IReadOnlyDictionary<string, string> GetAllFiles() => _files;

    /// <summary>
    /// Loads file contents from a dictionary (e.g., after deserialization).
    /// Clears existing content.
    /// </summary>
    internal void LoadFrom(IEnumerable<KeyValuePair<string, string>> files)
    {
        _files.Clear();
        _directories.Clear();
        foreach (var (key, value) in files)
        {
            var norm = Normalize(key);
            _files[norm] = value;
            EnsureParentDirectories(norm);
        }
    }
}
