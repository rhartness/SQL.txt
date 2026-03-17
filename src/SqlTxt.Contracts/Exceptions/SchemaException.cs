namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Exception thrown when schema operations fail (e.g., table not found, column mismatch).
/// </summary>
public sealed class SchemaException : SqlTxtException
{
    public SchemaException(string message)
        : base(message)
    {
    }

    public SchemaException(string message, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message, fileName, rowNumber, characterPosition)
    {
    }
}
