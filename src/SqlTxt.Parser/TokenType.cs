namespace SqlTxt.Parser;

/// <summary>
/// Types of tokens produced by the tokenizer.
/// </summary>
public enum TokenType
{
    End,
    Keyword,
    Identifier,
    StringLiteral,
    NumberLiteral,
    LeftParen,
    RightParen,
    Comma,
    Dot,
    Equals,
    Semicolon,
    Asterisk,
    BracketedIdentifier,
    GreaterThan,
    LessThan,
}
