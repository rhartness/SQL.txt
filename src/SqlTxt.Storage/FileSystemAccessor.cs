using System.Runtime.CompilerServices;

namespace SqlTxt.Storage;

/// <summary>
/// Default implementation of IFileSystemAccessor using the real file system.
/// </summary>
public sealed class FileSystemAccessor : Contracts.IFileSystemAccessor
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

    public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);

    public async Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        await File.AppendAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);

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

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath, overwrite: true);

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
