namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Phase 4 SELECT extensions (JOIN, ORDER BY, GROUP BY, subqueries). Null on <see cref="SelectCommand"/> means legacy single-table SELECT only.
/// </summary>
public sealed record SelectPhase4Extensions(
    string? FromAlias = null,
    IReadOnlyList<(string? Qualifier, string Column, string Value)>? WhereAndChain = null,
    IReadOnlyList<JoinOnSpecification>? Joins = null,
    IReadOnlyList<OrderByEntry>? OrderBy = null,
    IReadOnlyList<string>? GroupBy = null,
    IReadOnlyList<ProjectionItem>? Projections = null,
    HavingPredicate? Having = null,
    WhereInSpecification? WhereIn = null,
    WhereExistsSpecification? WhereExists = null);

public enum SqlJoinKind { Inner, Left }

/// <summary>
/// JOIN rhs with ON lhsCol = rhsCol (qualified by table or alias).
/// </summary>
public sealed record JoinOnSpecification(
    SqlJoinKind Kind,
    string RightTable,
    string? RightAlias,
    string LeftQualifier,
    string LeftColumn,
    string RightQualifier,
    string RightColumn);

public sealed record OrderByEntry(string Qualifier, string Column, bool Ascending);

public enum ProjectionKind
{
    Column,
    CountStar,
    CountColumn,
    Sum,
    Avg,
    Min,
    Max,
    ScalarSubquery
}

/// <summary>
/// One entry in the SELECT list (Phase 4). <see cref="OutputName"/> is the result column header.
/// </summary>
public sealed record ProjectionItem(
    ProjectionKind Kind,
    string? TableQualifier,
    string? ColumnName,
    string OutputName,
    ScalarSubquerySpec? ScalarSub = null);

/// <summary>
/// HAVING on a single aggregate: e.g. COUNT(*) &gt; 1, SUM(Amount) &gt; 0.
/// </summary>
public sealed record HavingPredicate(
    ProjectionKind AggregateKind,
    string? AggregateColumn,
    string Op,
    string Literal);

/// <summary>
/// WHERE col IN (subquery). Subquery must be a simple SELECT one column FROM table.
/// </summary>
public sealed record WhereInSpecification(string ColumnName, SelectCommand Subquery, string? ColumnQualifier = null);

/// <summary>
/// WHERE [NOT] EXISTS (SELECT 1 FROM InnerTable alias WHERE alias.InnerCol = outerQual.outerCol).
/// </summary>
public sealed record WhereExistsSpecification(
    bool NotExists,
    string InnerTable,
    string InnerAlias,
    string InnerColumn,
    string OuterQualifier,
    string OuterColumn);

/// <summary>
/// Correlated scalar subquery: (SELECT AGG(*) or AGG(col) FROM InnerTable alias WHERE alias.InnerEqCol = outerQual.outerCol).
/// </summary>
public sealed record ScalarSubquerySpec(
    string InnerTable,
    string InnerAlias,
    ProjectionKind AggregateKind,
    string? AggregateColumn,
    string InnerEqColumn,
    string OuterQualifier,
    string OuterColumn);
