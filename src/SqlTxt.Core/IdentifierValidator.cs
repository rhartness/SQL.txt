using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Core;

/// <summary>
/// Validates named object identifiers (tables, columns, schemas, databases).
/// Tables/schemas/databases: letters, digits, underscore only; cannot start with digit; cannot be reserved keyword.
/// Columns: same rules, or may use [brackets] for names with spaces.
/// </summary>
public static class IdentifierValidator
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "DATABASE", "TABLE", "WITH", "INSERT", "INTO", "VALUES",
        "SELECT", "FROM", "WHERE", "UPDATE", "SET", "DELETE",
        "CHAR", "INT", "TINYINT", "BIGINT", "BIT", "DECIMAL",
        "NUMBERFORMAT", "TEXTENCODING", "GO"
    };


    /// <summary>
    /// Validates a table, schema, or database name. Use only letters, digits, underscores.
    /// </summary>
    public static void ValidateTableName(string name, string objectType = "Table")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException($"{objectType} name cannot be empty.");

        if (char.IsDigit(name[0]))
            throw new ValidationException($"{objectType} name '{name}' is invalid: cannot start with a digit. Use only letters, digits, and underscores.");

        if (ReservedKeywords.Contains(name))
            throw new ValidationException($"{objectType} name '{name}' is invalid: reserved keyword. Choose a different name.");

        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                var charDesc = ch switch
                {
                    ' ' => "space",
                    '\n' => "newline",
                    '\r' => "carriage return",
                    '\t' => "tab",
                    '\'' => "single quote",
                    '"' => "double quote",
                    _ => $"'{ch}'"
                };
                throw new ValidationException($"{objectType} name '{name}' contains invalid character: {charDesc}. Use only letters, digits, and underscores.");
            }
        }
    }

    /// <summary>
    /// Validates a column name. Same rules as table names, or the name may have been extracted from [brackets] (spaces allowed inside).
    /// </summary>
    /// <param name="name">Column name (already extracted from [brackets] if applicable).</param>
    /// <param name="fromBrackets">True if name came from [Name] (allows spaces).</param>
    public static void ValidateColumnName(string name, bool fromBrackets = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Column name cannot be empty.");

        if (fromBrackets)
        {
            foreach (var ch in name)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != ' ')
                {
                    var charDesc = ch switch
                    {
                        '\n' => "newline",
                        '\r' => "carriage return",
                        '\t' => "tab",
                        '\'' => "single quote",
                        '"' => "double quote",
                        '[' => "left bracket",
                        ']' => "right bracket",
                        _ => $"'{ch}'"
                    };
                    throw new ValidationException($"Column name '{name}' contains invalid character: {charDesc}. Inside [brackets], only letters, digits, underscores, and spaces are allowed.");
                }
            }
            return;
        }

        if (char.IsDigit(name[0]))
            throw new ValidationException($"Column name '{name}' is invalid: cannot start with a digit. Use only letters, digits, and underscores, or use [Column Name] for names with spaces.");

        if (ReservedKeywords.Contains(name))
            throw new ValidationException($"Column name '{name}' is invalid: reserved keyword. Choose a different name or use [Column Name].");

        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                var charDesc = ch switch
                {
                    ' ' => "space (use [Column Name] for names with spaces)",
                    '\n' => "newline",
                    '\r' => "carriage return",
                    '\t' => "tab",
                    '\'' => "single quote",
                    '"' => "double quote",
                    _ => $"'{ch}'"
                };
                throw new ValidationException($"Column name '{name}' contains invalid character: {charDesc}. Use only letters, digits, and underscores, or use [Column Name] for names with spaces.");
            }
        }
    }
}
