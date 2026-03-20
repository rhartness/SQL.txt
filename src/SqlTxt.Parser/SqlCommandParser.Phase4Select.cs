using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Core;

namespace SqlTxt.Parser;

public sealed partial class SqlCommandParser
{
    private SelectCommand ParseSelectPhase4()
    {
        List<string>? simpleCols = null;
        List<ProjectionItem>? projections = null;

        if (Peek().Type == TokenType.Asterisk)
        {
            Advance();
        }
        else
        {
            var raw = ParseProjectionItems();
            if (raw.All(p => p.Kind == ProjectionKind.Column
                            && string.IsNullOrEmpty(p.TableQualifier)
                            && string.Equals(p.OutputName, p.ColumnName, StringComparison.OrdinalIgnoreCase)))
            {
                simpleCols = raw.Select(p => p.ColumnName!).ToList();
            }
            else
                projections = raw;
        }

        ExpectKeyword("FROM");
        var fromTable = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(fromTable);
        string? fromAlias = TryParseTableAlias();

        var joins = ParseJoinList();
        var withNoLock = TryParseWithNoLock();

        WhereParseResult whereRes = ParseWhereClause(fromAlias ?? fromTable, joins);

        List<string>? groupBy = null;
        if (PeekKeyword("GROUP"))
        {
            Advance();
            ExpectKeyword("BY");
            groupBy = ParseIdentifierListQualified();
        }

        HavingPredicate? having = null;
        if (PeekKeyword("HAVING"))
        {
            Advance();
            having = ParseHavingPredicate();
        }

        List<OrderByEntry>? orderBy = null;
        if (PeekKeyword("ORDER"))
        {
            Advance();
            ExpectKeyword("BY");
            orderBy = ParseOrderByList();
        }

        OptionalSemicolon();

        var chainForExt = whereRes.AndChain;
        var putChain = chainForExt is { Count: > 1 }
            || (joins.Count > 0 && chainForExt is { Count: > 0 })
            || whereRes.In != null
            || whereRes.Exists != null
            || (chainForExt is { Count: 1 } && chainForExt[0].Qual is not null);

        SelectPhase4Extensions? ext = null;
        if (fromAlias != null || joins.Count > 0 || putChain || whereRes.In != null || whereRes.Exists != null
            || groupBy != null || having != null || orderBy != null || projections != null)
        {
            ext = new SelectPhase4Extensions(
                FromAlias: fromAlias,
                WhereAndChain: putChain ? chainForExt : null,
                Joins: joins.Count > 0 ? joins : null,
                OrderBy: orderBy,
                GroupBy: groupBy,
                Projections: projections,
                Having: having,
                WhereIn: whereRes.In,
                WhereExists: whereRes.Exists);
        }

        return new SelectCommand(
            fromTable,
            simpleCols?.Count > 0 ? simpleCols : null,
            whereRes.LegacyColumn,
            whereRes.LegacyValue,
            withNoLock,
            ext);
    }

    private sealed class WhereParseResult
    {
        public List<(string? Qual, string Col, string Val)>? AndChain;
        public string? LegacyColumn;
        public string? LegacyValue;
        public WhereInSpecification? In;
        public WhereExistsSpecification? Exists;
    }

    private WhereParseResult ParseWhereClause(string defaultLeftQual, IReadOnlyList<JoinOnSpecification> joins)
    {
        var res = new WhereParseResult();
        if (!PeekKeyword("WHERE"))
            return res;

        Advance();
        if (PeekKeyword("EXISTS"))
        {
            res.Exists = ParseExistsPredicate(defaultLeftQual, joins);
            return res;
        }

        var (wq, wc) = ParseQualifiedColumn();
        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("IN", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            Expect(TokenType.LeftParen);
            var sub = ParseSimpleSubquerySelect();
            Expect(TokenType.RightParen);
            res.In = new WhereInSpecification(wc, sub, string.IsNullOrEmpty(wq) ? null : wq);
            return res;
        }

        // equality chain: qual.col = literal [AND ...]
        var chain = new List<(string? Qual, string Col, string Val)>();
        Expect(TokenType.Equals);
        var firstLit = ParseSqlLiteral();
        chain.Add((string.IsNullOrEmpty(wq) ? null : wq, wc, firstLit));
        while (Peek().Type == TokenType.Keyword && Peek().Value.Equals("AND", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            (wq, wc) = ParseQualifiedColumn();
            Expect(TokenType.Equals);
            var lit = ParseSqlLiteral();
            chain.Add((string.IsNullOrEmpty(wq) ? null : wq, wc, lit));
        }

        if (chain.Count == 1 && chain[0].Qual is null)
        {
            res.LegacyColumn = chain[0].Col;
            res.LegacyValue = chain[0].Val;
        }

        res.AndChain = chain;
        return res;
    }

    private WhereExistsSpecification ParseExistsPredicate(string defaultLeftQual, IReadOnlyList<JoinOnSpecification> joins)
    {
        ExpectKeyword("EXISTS");
        Expect(TokenType.LeftParen);
        ExpectKeyword("SELECT");
        if (Peek().Type == TokenType.NumberLiteral)
            Advance();
        else if (Peek().Type == TokenType.Asterisk)
            Advance();
        else
            ExpectIdentifier();
        ExpectKeyword("FROM");
        var innerTable = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(innerTable);
        var innerAlias = TryParseTableAlias() ?? innerTable;
        ExpectKeyword("WHERE");
        var (lq, lc) = ParseQualifiedColumn();
        Expect(TokenType.Equals);
        var (rq, rc) = ParseQualifiedColumn();
        Expect(TokenType.RightParen);
        return new WhereExistsSpecification(false, innerTable, innerAlias, lc, rq, rc);
    }

    private SelectCommand ParseSimpleSubquerySelect()
    {
        ExpectKeyword("SELECT");
        var items = ParseProjectionItems();
        if (items.Count != 1 || items[0].Kind != ProjectionKind.Column)
            throw ParseError("Subquery must be SELECT single column", Peek().Line, Peek().Column);
        var col = items[0].ColumnName!;
        ExpectKeyword("FROM");
        var tbl = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(tbl);
        var alias = TryParseTableAlias();
        List<(string? Qual, string Col, string Val)>? chain = null;
        if (PeekKeyword("WHERE"))
        {
            Advance();
            chain = new List<(string? Qual, string Col, string Val)>();
            while (true)
            {
                var (q, c) = ParseQualifiedColumn();
                Expect(TokenType.Equals);
                var lit = ParseSqlLiteral();
                chain.Add((string.IsNullOrEmpty(q) ? null : q, c, lit));
                if (!(Peek().Type == TokenType.Keyword && Peek().Value.Equals("AND", StringComparison.OrdinalIgnoreCase)))
                    break;
                Advance();
            }
        }

        SelectPhase4Extensions? subExt = null;
        if (alias != null || chain != null)
            subExt = new SelectPhase4Extensions(FromAlias: alias, WhereAndChain: chain);
        return new SelectCommand(tbl, new[] { col }, null, null, false, subExt);
    }

    private HavingPredicate ParseHavingPredicate()
    {
        var (kind, col) = ParseAggregateHead();
        var opTok = Peek();
        string op;
        if (opTok.Type == TokenType.GreaterThan) { Advance(); op = ">"; }
        else if (opTok.Type == TokenType.LessThan) { Advance(); op = "<"; }
        else if (opTok.Type == TokenType.Equals) { Advance(); op = "="; }
        else
            throw ParseError("Expected comparison in HAVING", opTok.Line, opTok.Column);
        var lit = ParseSqlLiteral();
        return new HavingPredicate(kind, col, op, lit);
    }

    private (ProjectionKind Kind, string? Col) ParseAggregateHead()
    {
        var t = Peek();
        if (t.Type != TokenType.Keyword)
            throw ParseError("Expected aggregate in HAVING", t.Line, t.Column);
        var fn = t.Value.ToUpperInvariant();
        Advance();
        Expect(TokenType.LeftParen);
        if (fn == "COUNT")
        {
            if (Peek().Type == TokenType.Asterisk)
            {
                Advance();
                Expect(TokenType.RightParen);
                return (ProjectionKind.CountStar, null);
            }
            var (c, _, _, _) = ExpectIdentifierOrBracketed();
            Expect(TokenType.RightParen);
            return (ProjectionKind.CountColumn, c);
        }
        if (fn == "SUM")
        {
            var (c, _, _, _) = ExpectIdentifierOrBracketed();
            Expect(TokenType.RightParen);
            return (ProjectionKind.Sum, c);
        }
        if (fn == "AVG")
        {
            var (c, _, _, _) = ExpectIdentifierOrBracketed();
            Expect(TokenType.RightParen);
            return (ProjectionKind.Avg, c);
        }
        if (fn == "MIN")
        {
            var (c, _, _, _) = ExpectIdentifierOrBracketed();
            Expect(TokenType.RightParen);
            return (ProjectionKind.Min, c);
        }
        if (fn == "MAX")
        {
            var (c, _, _, _) = ExpectIdentifierOrBracketed();
            Expect(TokenType.RightParen);
            return (ProjectionKind.Max, c);
        }
        throw ParseError("Unsupported HAVING aggregate", t.Line, t.Column);
    }

    private List<OrderByEntry> ParseOrderByList()
    {
        var list = new List<OrderByEntry>();
        while (true)
        {
            var (q, c) = ParseQualifiedColumn();
            var asc = true;
            if (PeekKeyword("DESC"))
            {
                asc = false;
                Advance();
            }
            else if (PeekKeyword("ASC"))
                Advance();
            list.Add(new OrderByEntry(string.IsNullOrEmpty(q) ? "" : q, c, asc));
            if (Peek().Type != TokenType.Comma)
                break;
            Advance();
        }
        return list;
    }

    private List<string> ParseIdentifierListQualified()
    {
        var list = new List<string>();
        while (true)
        {
            var (_, c) = ParseQualifiedColumn();
            list.Add(c);
            if (Peek().Type != TokenType.Comma)
                break;
            Advance();
        }
        return list;
    }

    private List<JoinOnSpecification> ParseJoinList()
    {
        var joins = new List<JoinOnSpecification>();
        while (true)
        {
            SqlJoinKind kind;
            if (PeekKeyword("INNER"))
            {
                Advance();
                ExpectKeyword("JOIN");
                kind = SqlJoinKind.Inner;
            }
            else if (PeekKeyword("LEFT"))
            {
                Advance();
                if (PeekKeyword("OUTER"))
                    Advance();
                ExpectKeyword("JOIN");
                kind = SqlJoinKind.Left;
            }
            else
                break;

            var rightTable = ExpectIdentifier().Name;
            IdentifierValidator.ValidateTableName(rightTable);
            var rightAlias = TryParseTableAlias() ?? rightTable;
            ExpectKeyword("ON");
            var (lq, lc) = ParseQualifiedColumn();
            Expect(TokenType.Equals);
            var (rq, rc) = ParseQualifiedColumn();
            joins.Add(new JoinOnSpecification(kind, rightTable, rightAlias == rightTable ? null : rightAlias, lq, lc, rq, rc));
        }
        return joins;
    }

    private List<ProjectionItem> ParseProjectionItems()
    {
        var list = new List<ProjectionItem>();
        while (true)
        {
            if (Peek().Type == TokenType.LeftParen)
            {
                Advance();
                ExpectKeyword("SELECT");
                var scalar = ParseScalarSubqueryBody();
                Expect(TokenType.RightParen);
                string alias;
                if (PeekKeyword("AS"))
                {
                    Advance();
                    alias = ExpectIdentifier().Name;
                }
                else
                    alias = ExpectIdentifier().Name;
                list.Add(new ProjectionItem(ProjectionKind.ScalarSubquery, null, null, alias, scalar));
            }
            else if (PeekKeyword("COUNT") || PeekKeyword("SUM") || PeekKeyword("AVG") || PeekKeyword("MIN") || PeekKeyword("MAX"))
            {
                list.Add(ParseAggregateProjectionItem());
            }
            else
            {
                var (q, c) = ParseQualifiedColumn();
                IdentifierValidator.ValidateColumnName(c, false);
                var output = c;
                if (PeekKeyword("AS"))
                {
                    Advance();
                    output = ExpectIdentifier().Name;
                }
                list.Add(new ProjectionItem(ProjectionKind.Column, string.IsNullOrEmpty(q) ? null : q, c, output));
            }

            if (Peek().Type != TokenType.Comma)
                break;
            Advance();
        }
        return list;
    }

    private ScalarSubquerySpec ParseScalarSubqueryBody()
    {
        var (kind, argCol) = ParseAggregateHead();
        ExpectKeyword("FROM");
        var innerTable = ExpectIdentifier().Name;
        IdentifierValidator.ValidateTableName(innerTable);
        var innerAlias = TryParseTableAlias() ?? innerTable;
        ExpectKeyword("WHERE");
        var (lq, lc) = ParseQualifiedColumn();
        Expect(TokenType.Equals);
        var (rq, rc) = ParseQualifiedColumn();
        return new ScalarSubquerySpec(innerTable, innerAlias, kind, argCol, lc, rq, rc);
    }

    private ProjectionItem ParseAggregateProjectionItem()
    {
        var t = ExpectKeyword().Value.ToUpperInvariant();
        Expect(TokenType.LeftParen);
        ProjectionKind k;
        string? col = null;
        if (t == "COUNT")
        {
            if (Peek().Type == TokenType.Asterisk)
            {
                Advance();
                k = ProjectionKind.CountStar;
            }
            else
            {
                (col, _, _, _) = ExpectIdentifierOrBracketed();
                k = ProjectionKind.CountColumn;
            }
        }
        else if (t == "SUM")
        {
            (col, _, _, _) = ExpectIdentifierOrBracketed();
            k = ProjectionKind.Sum;
        }
        else if (t == "AVG")
        {
            (col, _, _, _) = ExpectIdentifierOrBracketed();
            k = ProjectionKind.Avg;
        }
        else if (t == "MIN")
        {
            (col, _, _, _) = ExpectIdentifierOrBracketed();
            k = ProjectionKind.Min;
        }
        else if (t == "MAX")
        {
            (col, _, _, _) = ExpectIdentifierOrBracketed();
            k = ProjectionKind.Max;
        }
        else
            throw ParseError("Bad aggregate", Peek().Line, Peek().Column);

        Expect(TokenType.RightParen);
        string output;
        if (PeekKeyword("AS"))
        {
            Advance();
            output = ExpectIdentifier().Name;
        }
        else
            output = t + "_" + (col ?? "*");

        return new ProjectionItem(k, null, col, output);
    }

    private (string Qual, string Col) ParseQualifiedColumn()
    {
        var (first, _, _, _) = ExpectIdentifierOrBracketed();
        if (Peek().Type == TokenType.Dot)
        {
            Advance();
            var (second, _, _, _) = ExpectIdentifierOrBracketed();
            return (first, second);
        }
        return ("", first);
    }

    private string ParseSqlLiteral()
    {
        var p = Peek();
        if (p.Type == TokenType.StringLiteral)
        {
            Advance();
            return p.Value;
        }
        if (p.Type == TokenType.NumberLiteral)
        {
            Advance();
            return p.Value;
        }
        throw ParseError("Expected literal", p.Line, p.Column);
    }

    private string? TryParseTableAlias()
    {
        var p = Peek();
        if (p.Type == TokenType.Keyword)
            return null;
        if (p.Type != TokenType.Identifier)
            return null;
        var name = p.Value;
        Advance();
        return name;
    }

    private bool TryParseWithNoLock()
    {
        if (Peek().Type == TokenType.Keyword && Peek().Value.Equals("WITH", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            Expect(TokenType.LeftParen);
            ExpectKeyword("NOLOCK");
            Expect(TokenType.RightParen);
            return true;
        }
        return false;
    }

    private bool PeekKeyword(string kw) =>
        Peek().Type == TokenType.Keyword && Peek().Value.Equals(kw, StringComparison.OrdinalIgnoreCase);
}
