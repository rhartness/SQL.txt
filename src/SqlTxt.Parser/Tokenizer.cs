using System.Text;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Parser;

/// <summary>
/// Tokenizes SQL input into tokens.
/// </summary>
public sealed class Tokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "CREATE", "DATABASE", "TABLE", "WITH", "INSERT", "INTO", "VALUES",
        "SELECT", "FROM", "WHERE", "UPDATE", "SET", "DELETE",
        "CHAR", "INT", "TINYINT", "BIGINT", "BIT", "DECIMAL",
        "NUMBERFORMAT", "TEXTENCODING",
        "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "UNIQUE",
        "INDEX", "ON", "NOLOCK"
    };

    private readonly string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    public Tokenizer(string input)
    {
        _input = input ?? string.Empty;
    }

    public Token Next()
    {
        SkipWhitespace();

        if (_position >= _input.Length)
            return new Token(TokenType.End, "", _line, _column);

        var startLine = _line;
        var startCol = _column;
        var ch = _input[_position];

        if (ch == '\'')
            return ReadStringLiteral(startLine, startCol);
        if (ch == '(')
            return Consume(TokenType.LeftParen, "(", startLine, startCol);
        if (ch == ')')
            return Consume(TokenType.RightParen, ")", startLine, startCol);
        if (ch == ',')
            return Consume(TokenType.Comma, ",", startLine, startCol);
        if (ch == ';')
            return Consume(TokenType.Semicolon, ";", startLine, startCol);
        if (ch == '*')
            return Consume(TokenType.Asterisk, "*", startLine, startCol);
        if (ch == '=')
            return Consume(TokenType.Equals, "=", startLine, startCol);
        if (ch == '[')
            return ReadBracketedIdentifier(startLine, startCol);

        if (char.IsLetter(ch) || ch == '_')
            return ReadIdentifierOrKeyword(startLine, startCol);
        if (char.IsDigit(ch))
            return ReadNumber(startLine, startCol);

        Advance();
        return new Token(TokenType.End, "", startLine, startCol);
    }

    public IEnumerable<Token> ReadAll()
    {
        while (true)
        {
            var t = Next();
            if (t.Type == TokenType.End)
                break;
            yield return t;
        }
    }

    private void SkipWhitespace()
    {
        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (ch == ' ' || ch == '\t')
            {
                Advance();
            }
            else if (ch == '-' && _position + 1 < _input.Length && _input[_position + 1] == '-')
            {
                while (_position < _input.Length && _input[_position] != '\n' && _input[_position] != '\r')
                    Advance();
            }
            else if (ch == '\n')
            {
                Advance();
                _line++;
                _column = 1;
            }
            else if (ch == '\r')
            {
                Advance();
                if (_position < _input.Length && _input[_position] == '\n')
                    Advance();
                _line++;
                _column = 1;
            }
            else
            {
                break;
            }
        }
    }

    private void Advance()
    {
        if (_position < _input.Length)
        {
            _position++;
            _column++;
        }
    }

    private Token Consume(TokenType type, string value, int line, int col)
    {
        Advance();
        return new Token(type, value, line, col);
    }

    private Token ReadStringLiteral(int startLine, int startCol)
    {
        Advance(); // skip opening quote
        var sb = new StringBuilder();

        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (ch == '\'')
            {
                Advance();
                return new Token(TokenType.StringLiteral, sb.ToString(), startLine, startCol);
            }
            if (ch == '\\' && _position + 1 < _input.Length)
            {
                Advance();
                var next = _input[_position];
                Advance();
                sb.Append(next switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    _ => next
                });
            }
            else
            {
                sb.Append(ch);
                Advance();
            }
        }

        return new Token(TokenType.StringLiteral, sb.ToString(), startLine, startCol);
    }

    private Token ReadIdentifierOrKeyword(int startLine, int startCol)
    {
        var sb = new StringBuilder();
        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                sb.Append(ch);
                Advance();
            }
            else
                break;
        }

        var value = sb.ToString();
        var type = Keywords.Contains(value) ? TokenType.Keyword : TokenType.Identifier;
        return new Token(type, value, startLine, startCol);
    }

    private Token ReadBracketedIdentifier(int startLine, int startCol)
    {
        Advance(); // skip [
        var sb = new StringBuilder();
        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (ch == ']')
            {
                Advance();
                return new Token(TokenType.BracketedIdentifier, sb.ToString(), startLine, startCol);
            }
            if (ch == '\n' || ch == '\r')
                throw new ParseException($"Unclosed bracketed identifier: missing closing ']' before newline.", startLine, startCol);
            sb.Append(ch);
            Advance();
        }
        throw new ParseException($"Unclosed bracketed identifier: missing closing ']'.", startLine, startCol);
    }

    private Token ReadNumber(int startLine, int startCol)
    {
        var sb = new StringBuilder();
        while (_position < _input.Length)
        {
            var ch = _input[_position];
            if (char.IsDigit(ch) || ch == '.')
            {
                sb.Append(ch);
                Advance();
            }
            else
                break;
        }
        return new Token(TokenType.NumberLiteral, sb.ToString(), startLine, startCol);
    }
}
