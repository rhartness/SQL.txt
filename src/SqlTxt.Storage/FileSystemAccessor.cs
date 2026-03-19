using System.Runtime.CompilerServices;

namespace SqlTxt.Storage;

/// <summary>
/// Default implementation of IFileSystemAccessor using the real file system.
/// Uses retry-with-backoff for transient I/O errors (e.g., "Access to the path is denied" on Windows).
/// </summary>
public sealed class FileSystemAccessor : Contracts.IFileSystemAccessor
{
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = [10, 50, 200];

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await File.AppendAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

    public async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task AppendAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<IReadOnlyList<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default)
    {
        var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        return lines;
    }

    public async IAsyncEnumerable<string> ReadLinesAsync(string path, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                yield return line;
            }
        }
    }

    public async Task<Stream> OpenReadStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536, useAsync: true);
                return await Task.FromResult<Stream>(stream).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
        throw new IOException($"Failed to open read stream after {MaxRetries} attempts: {path}");
    }

    public async Task<Stream> OpenWriteStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);
                return await Task.FromResult<Stream>(stream).ConfigureAwait(false);
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                await Task.Delay(RetryDelaysMs[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
        throw new IOException($"Failed to open write stream after {MaxRetries} attempts: {path}");
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
            catch (UnauthorizedAccessException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelaysMs[attempt]);
            }
        }
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public string Combine(params string[] paths) => Path.Combine(paths);

    public IReadOnlyList<string> GetDirectories(string path) =>
        Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>();

    public IReadOnlyList<string> GetFiles(string path) =>
        Directory.Exists(path) ? Directory.GetFiles(path) : Array.Empty<string>();

    public long GetFileLength(string path) => new FileInfo(path).Length;

    public void DeleteDirectory(string path) => Directory.Delete(path, recursive: true);
}
