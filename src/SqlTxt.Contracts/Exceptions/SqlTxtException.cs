namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Base exception for all SQL.txt errors.
/// </summary>
public class SqlTxtException : Exception
{
    /// <summary>
    /// File name where the error occurred, if applicable.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Row number where the error occurred, if applicable.
    /// </summary>
    public int? RowNumber { get; }

    /// <summary>
    /// Character position where the error occurred, if applicable.
    /// </summary>
    public int? CharacterPosition { get; }

    public SqlTxtException(string message)
        : base(message)
    {
    }

    public SqlTxtException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SqlTxtException(string message, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message)
    {
        FileName = fileName;
        RowNumber = rowNumber;
        CharacterPosition = characterPosition;
    }

    public SqlTxtException(string message, Exception innerException, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message, innerException)
    {
        FileName = fileName;
        RowNumber = rowNumber;
        CharacterPosition = characterPosition;
    }
}
