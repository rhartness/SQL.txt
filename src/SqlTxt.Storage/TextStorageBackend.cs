using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Text storage backend: human-readable files with .txt extension.
/// </summary>
public sealed class TextStorageBackend : IStorageBackend
{
    public string DataFileExtension => ".txt";
    public string IndexFileExtension => ".txt";
    public string SchemaFileExtension => ".txt";
    public bool IsTextBackend => true;
}
