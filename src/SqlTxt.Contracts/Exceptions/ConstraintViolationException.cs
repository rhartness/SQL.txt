namespace SqlTxt.Contracts.Exceptions;

/// <summary>
/// Exception thrown when a constraint is violated (e.g., primary key, foreign key).
/// </summary>
public sealed class ConstraintViolationException : SqlTxtException
{
    public ConstraintViolationException(string message)
        : base(message)
    {
    }

    public ConstraintViolationException(string message, string? fileName = null, int? rowNumber = null, int? characterPosition = null)
        : base(message, fileName, rowNumber, characterPosition)
    {
    }
}
