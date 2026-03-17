namespace SqlTxt.Contracts;

/// <summary>
/// Abstraction for file system operations (enables testability).
/// </summary>
public interface IFileSystemAccessor
{
    /// <summary>
    /// Creates a directory if it does not exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Checks if a directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes all text to a file.
    /// </summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends text to a file.
    /// </summary>
    Task AppendAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all lines from a file.
    /// </summary>
    Task<IReadOnlyList<string>> ReadAllLinesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full path, resolving relative paths against current directory.
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// Combines path segments in a cross-platform way.
    /// </summary>
    string Combine(params string[] paths);

    /// <summary>
    /// Gets all subdirectories in a path.
    /// </summary>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>
    /// Gets all files in a directory.
    /// </summary>
    IReadOnlyList<string> GetFiles(string path);

    /// <summary>
    /// Gets file size in bytes.
    /// </summary>
    long GetFileLength(string path);

    /// <summary>
    /// Deletes a directory and all its contents recursively.
    /// </summary>
    void DeleteDirectory(string path);
}
