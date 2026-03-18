using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Binary storage backend: compact files with .bin and .idx extensions.
/// Stub for Plan B; full implementation in Plan C.
/// </summary>
public sealed class BinaryStorageBackend : IStorageBackend
{
    public string DataFileExtension => ".bin";
    public string IndexFileExtension => ".idx";
    public string SchemaFileExtension => ".bin";
    public bool IsTextBackend => false;
}
