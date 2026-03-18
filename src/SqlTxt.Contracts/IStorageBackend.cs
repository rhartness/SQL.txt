namespace SqlTxt.Contracts;

/// <summary>
/// Abstraction for storage backend (text vs binary). Provides file extensions and format selection.
/// </summary>
public interface IStorageBackend
{
    /// <summary>
    /// File extension for table data files (e.g., ".txt" or ".bin").
    /// </summary>
    string DataFileExtension { get; }

    /// <summary>
    /// File extension for index files (e.g., ".txt" or ".idx").
    /// </summary>
    string IndexFileExtension { get; }

    /// <summary>
    /// File extension for schema files (e.g., ".txt" or ".bin").
    /// </summary>
    string SchemaFileExtension { get; }

    /// <summary>
    /// True if this is the text (human-readable) backend; false for binary.
    /// </summary>
    bool IsTextBackend { get; }
}
