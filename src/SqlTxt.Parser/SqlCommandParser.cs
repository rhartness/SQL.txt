using SqlTxt.Contracts;
using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Core;

namespace SqlTxt.Parser;

/// <summary>
/// Parses SQL text into strongly typed command objects.
/// </summary>
public sealed class SqlCommandParser : ICommandParser
{
    private List<Token> _tokens = new();
    private int _position;

    public object Parse(string sql)
    {
        var tokenizer = new Tokenizer(sql);
        _tokens = tokenizer.ReadAll().ToList();
        _position = 0;

        if (_tokens.Count == 0)
            throw new ParseException("Empty statement", 1, 1);

        var cmd = ParseStatement();
        return cmd;
    }

    private object ParseStatement()
    {
        var t = ExpectKeyword();
        return t.Value.ToUpperInvariant() switch
        {
            "CREATE" => ParseCreate(),
            "INSERT" => ParseInsert(),
            "SELECT" => ParseSelect(),
            "UPDATE" => ParseUpdate(),
            "DELETE" => ParseDelete(),
            _ => throw ParseError($"Expected CREATE, INSERT, SELECT, UPDATE, or DELETE; got {t.Value}", t.Line, t.Column)
        };
    }

    private object ParseCreate()
    {
        var t = ExpectKeyword();
        return t.Value.ToUpperInvariant() switch
        {
            "DATABASE" => ParseCreateDatabase(),
            "TABLE" => ParseCreateTable(),
            _ => throw ParseError($"Expected DATABASE or TABLE after CREATE; got {t.Value}", t.Line, t.Column)
        };
    }

    private CreateDatabaseCommand ParseCreateDatabase()
    {
        var (name, line, col) = ExpectIdentifier();
        IdentifierValidator.ValidateTableName(name, "Database");
        string? numberFormat = null;
        string? textEncoding = null;

        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("WITH", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            Expect(TokenType.LeftParen);
            while (true)
            {
                var key = ExpectIdentifier().Name;
                Expect(TokenType.Equals);
                var val = Expect(TokenType.StringLiteral).Value;
                if (key.Equals("numberFormat", StringComparison.OrdinalIgnoreCase))
                    numberFormat = val;
                else if (key.Equals("textEncoding", StringComparison.OrdinalIgnoreCase))
                    textEncoding = val;

                if (Peek().Type == TokenType.RightParen)
                    break;
                Expect(TokenType.Comma);
            }
            Expect(TokenType.RightParen);
        }

        OptionalSemicolon();
        return new CreateDatabaseCommand(name, ".", numberFormat, textEncoding);
    }

    private CreateTableCommand ParseCreateTable()
    {
        var (tableName, _, _) = ExpectIdentifier();
        IdentifierValidator.ValidateTableName(tableName);
        Expect(TokenType.LeftParen);

        var columns = new List<ColumnDefinition>();
        while (true)
        {
            var (colName, _, _, fromBrackets) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(colName, fromBrackets);
            var colType = ParseColumnType();
            columns.Add(colType with { Name = colName });

            if (Peek().Type == TokenType.RightParen)
                break;
            Expect(TokenType.Comma);
        }

        Expect(TokenType.RightParen);
        OptionalSemicolon();

        var table = new TableDefinition(tableName, columns);
        return new CreateTableCommand(table);
    }

    private ColumnDefinition ParseCharType()
    {
        Expect(TokenType.LeftParen);
        var w = int.Parse(Expect(TokenType.NumberLiteral).Value);
        Expect(TokenType.RightParen);
        return new ColumnDefinition("", ColumnType.Char, w, null);
    }

    private ColumnDefinition ParseDecimalType()
    {
        Expect(TokenType.LeftParen);
        var p = int.Parse(Expect(TokenType.NumberLiteral).Value);
        Expect(TokenType.Comma);
        var s = int.Parse(Expect(TokenType.NumberLiteral).Value);
        Expect(TokenType.RightParen);
        return new ColumnDefinition("", ColumnType.Decimal, p, s);
    }

    private ColumnDefinition ParseColumnType()
    {
        var t = ExpectKeyword();
        return t.Value.ToUpperInvariant() switch
        {
            "CHAR" => ParseCharType(),
            "INT" => new ColumnDefinition("", ColumnType.Int, null, null),
            "TINYINT" => new ColumnDefinition("", ColumnType.TinyInt, null, null),
            "BIGINT" => new ColumnDefinition("", ColumnType.BigInt, null, null),
            "BIT" => new ColumnDefinition("", ColumnType.Bit, null, null),
            "DECIMAL" => ParseDecimalType(),
            _ => throw ParseError($"Expected column type; got {t.Value}", t.Line, t.Column)
        };
    }

    private InsertCommand ParseInsert()
    {
        ExpectKeyword("INTO");
        var tableName = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(tableName);
        Expect(TokenType.LeftParen);

        var columns = new List<string>();
        while (true)
        {
            var (colName, _, _, fromBrackets) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(colName, fromBrackets);
            columns.Add(colName);
            if (Peek().Type == TokenType.RightParen)
                break;
            Expect(TokenType.Comma);
        }
        Expect(TokenType.RightParen);
        ExpectKeyword("VALUES");
        Expect(TokenType.LeftParen);

        var values = new List<string>();
        while (true)
        {
            var p = Peek();
            if (p.Type == TokenType.StringLiteral)
            {
                Advance();
                values.Add(p.Value);
            }
            else if (p.Type == TokenType.NumberLiteral)
            {
                Advance();
                values.Add(p.Value);
            }
            else
                throw ParseError("Expected literal value", p.Line, p.Column);

            if (Peek().Type == TokenType.RightParen)
                break;
            Expect(TokenType.Comma);
        }
        Expect(TokenType.RightParen);
        OptionalSemicolon();

        return new InsertCommand(tableName, columns, values);
    }

    private SelectCommand ParseSelect()
    {
        var columns = new List<string>();
        if (Peek().Type == TokenType.Asterisk)
        {
            Advance();
            columns = null!; // SELECT *
        }
        else
        {
            while (true)
            {
                var (colName, _, _, fromBrackets) = ExpectIdentifierOrBracketed();
                IdentifierValidator.ValidateColumnName(colName, fromBrackets);
                columns.Add(colName);
                if (Peek().Type != TokenType.Comma)
                    break;
                Advance();
            }
        }

        ExpectKeyword("FROM");
        var tableName = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(tableName);

        string? whereCol = null;
        string? whereVal = null;
        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            var (wc, _, _, wcBracketed) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(wc, wcBracketed);
            whereCol = wc;
            Expect(TokenType.Equals);
            var p = Peek();
            if (p.Type == TokenType.StringLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else if (p.Type == TokenType.NumberLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else
                throw ParseError("Expected literal in WHERE clause", p.Line, p.Column);
        }

        OptionalSemicolon();
        return new SelectCommand(tableName, columns?.Count > 0 ? columns : null, whereCol, whereVal);
    }

    private UpdateCommand ParseUpdate()
    {
        var tableName = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(tableName);
        ExpectKeyword("SET");

        var setClauses = new List<(string, string)>();
        while (true)
        {
            var (col, _, _, colBracketed) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(col, colBracketed);
            Expect(TokenType.Equals);
            var p = Peek();
            string val;
            if (p.Type == TokenType.StringLiteral)
            {
                Advance();
                val = p.Value;
            }
            else if (p.Type == TokenType.NumberLiteral)
            {
                Advance();
                val = p.Value;
            }
            else
                throw ParseError("Expected literal value", p.Line, p.Column);

            setClauses.Add((col, val));

            if (Peek().Type != TokenType.Comma)
                break;
            Advance();
        }

        string? whereCol = null;
        string? whereVal = null;
        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            var (wc, _, _, wcBracketed) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(wc, wcBracketed);
            whereCol = wc;
            Expect(TokenType.Equals);
            var p = Peek();
            if (p.Type == TokenType.StringLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else if (p.Type == TokenType.NumberLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else
                throw ParseError("Expected literal in WHERE clause", p.Line, p.Column);
        }

        OptionalSemicolon();
        return new UpdateCommand(tableName, setClauses, whereCol, whereVal);
    }

    private DeleteCommand ParseDelete()
    {
        ExpectKeyword("FROM");
        var tableName = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(tableName);

        string? whereCol = null;
        string? whereVal = null;
        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            var (wc, _, _, wcBracketed) = ExpectIdentifierOrBracketed();
            IdentifierValidator.ValidateColumnName(wc, wcBracketed);
            whereCol = wc;
            Expect(TokenType.Equals);
            var p = Peek();
            if (p.Type == TokenType.StringLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else if (p.Type == TokenType.NumberLiteral)
            {
                Advance();
                whereVal = p.Value;
            }
            else
                throw ParseError("Expected literal in WHERE clause", p.Line, p.Column);
        }

        OptionalSemicolon();
        return new DeleteCommand(tableName, whereCol, whereVal);
    }

    private Token Peek() =>
        _position < _tokens.Count ? _tokens[_position] : new Token(TokenType.End, "", 0, 0);

    private void Advance()
    {
        if (_position < _tokens.Count)
            _position++;
    }

    private Token ExpectKeyword(string? expected = null)
    {
        var t = Peek();
        if (t.Type != TokenType.Keyword)
            throw ParseError(expected != null ? $"Expected keyword '{expected}'" : "Expected keyword", t.Line, t.Column);
        if (expected != null && !t.Value.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw ParseError($"Expected '{expected}'; got '{t.Value}'", t.Line, t.Column);
        Advance();
        return t;
    }

    private (string Name, int Line, int Column) ExpectIdentifier()
    {
        var t = Peek();
        if (t.Type != TokenType.Identifier && t.Type != TokenType.Keyword)
            throw ParseError("Expected identifier", t.Line, t.Column);
        Advance();
        return (t.Value, t.Line, t.Column);
    }

    private (string Name, int Line, int Column, bool FromBrackets) ExpectIdentifierOrBracketed()
    {
        var t = Peek();
        if (t.Type == TokenType.BracketedIdentifier)
        {
            Advance();
            return (t.Value, t.Line, t.Column, true);
        }
        if (t.Type == TokenType.Identifier || t.Type == TokenType.Keyword)
        {
            Advance();
            return (t.Value, t.Line, t.Column, false);
        }
        throw ParseError("Expected identifier or [bracketed identifier]", t.Line, t.Column);
    }

    private Token Expect(TokenType type)
    {
        var t = Peek();
        if (t.Type != type)
            throw ParseError($"Expected {type}; got {t.Type}", t.Line, t.Column);
        Advance();
        return t;
    }

    private void OptionalSemicolon()
    {
        if (Peek().Type == TokenType.Semicolon)
            Advance();
    }

    private static ParseException ParseError(string message, int line, int column) =>
        new(message, line, column);
}
