namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Exception thrown when validation fails (e.g., value too long for column).
/// </summary>
public sealed class ValidationException : SqlTxtException
{
    public ValidationException(string message)
        : base(message)
    {
    }

    public ValidationException(string message, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message, fileName, rowNumber, characterPosition)
    {
    }
}
