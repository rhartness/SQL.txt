namespace SqlTxt.Contracts;

/// <summary>
/// Parses SQL text into strongly typed command objects.
/// </summary>
public interface ICommandParser
{
    /// <summary>
    /// Parses a single SQL statement into a command object.
    /// </summary>
    /// <param name="sql">SQL statement text.</param>
    /// <returns>Parsed command (CreateDatabaseCommand, CreateTableCommand, etc.).</returns>
    /// <exception cref="Exceptions.ParseException">When parsing fails.</exception>
    object Parse(string sql);
}
