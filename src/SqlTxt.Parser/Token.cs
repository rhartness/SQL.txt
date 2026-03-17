namespace SqlTxt.Parser;

/// <summary>
/// A single token from the SQL input.
/// </summary>
/// <param name="Type">Token type.</param>
/// <param name="Value">Token value (for identifiers, literals, keywords).</param>
/// <param name="Line">1-based line number.</param>
/// <param name="Column">1-based column number.</param>
public readonly record struct Token(TokenType Type, string Value, int Line, int Column);
