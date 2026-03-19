namespace SqlTxt.Storage;

/// <summary>
/// Loads a filesystem-backed database into memory and saves it back to disk.
/// Used for load-into-memory mode: operate entirely in RAM, optionally flush on exit.
/// </summary>
public static class DatabaseLoader
{
    /// <summary>
    /// Loads all files from a database directory into a MemoryFileSystemAccessor.
    /// </summary>
    /// <param name="databasePath">Full path to the database directory (e.g., ./WikiDb).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A MemoryFileSystemAccessor with the database contents loaded. The virtual root is the database folder name.</returns>
    public static async Task<(MemoryFileSystemAccessor Memory, string VirtualRoot)> LoadIntoMemoryAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(databasePath);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException($"Database directory not found: {fullPath}");

        var dbName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(dbName))
            dbName = "Database";

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var keyPrefix = dbName + "/";
        await LoadDirectoryRecursiveAsync(fullPath, fullPath, keyPrefix, files, cancellationToken).ConfigureAwait(false);

        var memory = new MemoryFileSystemAccessor();
        var normalized = files.ToDictionary(kv => MemoryFileSystemAccessor.NormalizeKey(kv.Key), kv => kv.Value);
        memory.LoadFrom(normalized);

        return (memory, dbName);
    }

    /// <summary>
    /// Saves the contents of a MemoryFileSystemAccessor back to a database directory.
    /// </summary>
    /// <param name="memory">The in-memory database loaded via LoadIntoMemoryAsync.</param>
    /// <param name="databasePath">Full path to the target database directory.</param>
    /// <param name="virtualRoot">The virtual root prefix used when loading (e.g., "WikiDb").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SaveToDiskAsync(
        MemoryFileSystemAccessor memory,
        string databasePath,
        string virtualRoot,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(fullPath);

        var files = memory.GetAllFiles();
        var prefix = virtualRoot.TrimEnd('/') + "/";
        foreach (var (key, value) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var relativePath = key[prefix.Length..];
            var filePath = Path.Combine(fullPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, value, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task LoadDirectoryRecursiveAsync(
        string rootPath,
        string currentPath,
        string rootKeyPrefix,
        Dictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(rootPath, file);
            var key = rootKeyPrefix + relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            files[key] = content;
        }

        foreach (var dir in Directory.GetDirectories(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await LoadDirectoryRecursiveAsync(rootPath, dir, rootKeyPrefix, files, cancellationToken).ConfigureAwait(false);
        }
    }
}
