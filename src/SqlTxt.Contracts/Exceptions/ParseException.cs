namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Exception thrown when SQL parsing fails.
/// </summary>
public sealed class ParseException : SqlTxtException
{
    /// <summary>
    /// Line number in the input where the error occurred.
    /// </summary>
    public int? Line { get; }

    /// <summary>
    /// Column number in the input where the error occurred.
    /// </summary>
    public int? Column { get; }

    public ParseException(string message)
        : base(message)
    {
    }

    public ParseException(string message, int? line = null, int? column = null)
        : base(message, fileName: null, rowNumber: line, characterPosition: column)
    {
        Line = line;
        Column = column;
    }
}
