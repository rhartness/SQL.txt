namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Exception thrown when storage operations fail (e.g., file I/O, corrupted data).
/// </summary>
public sealed class StorageException : SqlTxtException
{
    public StorageException(string message)
        : base(message)
    {
    }

    public StorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public StorageException(string message, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message, fileName, rowNumber, characterPosition)
    {
    }
}
