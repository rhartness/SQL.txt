using System.Globalization;
using SqlTxt.Contracts;
using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Engine;

/// <summary>
/// Phase 4 SELECT: JOIN, compound WHERE, ORDER BY, GROUP BY / aggregates / HAVING, IN / EXISTS / scalar subqueries.
/// </summary>
public sealed partial class DatabaseEngine
{
    private static IReadOnlyList<string> CollectSelectInvolvedTables(SelectCommand cmd)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSelectTables(cmd, set);
        return set.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddSelectTables(SelectCommand cmd, HashSet<string> tables)
    {
        tables.Add(cmd.TableName);
        if (cmd.Extensions is not { } ext)
            return;
        if (ext.Joins != null)
        {
            foreach (var j in ext.Joins)
                tables.Add(j.RightTable);
        }

        if (ext.WhereIn?.Subquery is { } sq)
            AddSelectTables(sq, tables);
        if (ext.WhereExists != null)
            tables.Add(ext.WhereExists.InnerTable);
        if (ext.Projections != null)
        {
            foreach (var p in ext.Projections)
            {
                if (p.ScalarSub is { } ss)
                    tables.Add(ss.InnerTable);
            }
        }
    }

    private async Task<EngineResult> ExecuteSelectPhase4Async(
        string databasePath,
        SelectCommand cmd,
        long? mvccSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        var ext = cmd.Extensions!;
        var fromQual = ext.FromAlias ?? cmd.TableName;

        var rowCache = new Dictionary<string, List<RowData>>(StringComparer.OrdinalIgnoreCase);
        async Task<List<RowData>> RowsAsync(string tableName)
        {
            if (rowCache.TryGetValue(tableName, out var cached))
                return cached;
            var list = new List<RowData>();
            await foreach (var r in _tableDataStore
                               .ReadRowsAsync(databasePath, tableName, cancellationToken, mvccSnapshotCommitted)
                               .WithCancellation(cancellationToken)
                               .ConfigureAwait(false))
            {
                list.Add(r);
            }

            rowCache[tableName] = list;
            return list;
        }

        foreach (var tbl in CollectSelectInvolvedTables(cmd))
            await RowsAsync(tbl).ConfigureAwait(false);

        var qualOrder = new List<string> { fromQual };
        if (ext.Joins != null)
        {
            foreach (var j in ext.Joins)
                qualOrder.Add(j.RightAlias ?? j.RightTable);
        }

        static bool StrEq(string? a, string? b) =>
            string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

        string? GetCol(IReadOnlyDictionary<string, RowData?> ctx, string? qualifier, string column)
        {
            var q = string.IsNullOrEmpty(qualifier) ? fromQual : qualifier!;
            if (ctx.TryGetValue(q, out var row) && row != null)
            {
                var v = row.GetValue(column);
                if (v != null)
                    return v;
            }

            foreach (var qq in qualOrder)
            {
                if (string.Equals(qq, q, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ctx.TryGetValue(qq, out var r2) && r2 != null && r2.GetValue(column) is { } v2)
                    return v2;
            }

            return null;
        }

        var fromRows = await RowsAsync(cmd.TableName).ConfigureAwait(false);
        var contexts = new List<Dictionary<string, RowData?>>(fromRows.Count);
        foreach (var row in fromRows)
        {
            var d = new Dictionary<string, RowData?>(StringComparer.OrdinalIgnoreCase) { [fromQual] = row };
            contexts.Add(d);
        }

        if (ext.Joins != null)
        {
            foreach (var join in ext.Joins)
            {
                var rightQual = join.RightAlias ?? join.RightTable;
                var rightRows = await RowsAsync(join.RightTable).ConfigureAwait(false);
                var next = new List<Dictionary<string, RowData?>>();
                foreach (var ctx in contexts)
                {
                    if (!ctx.TryGetValue(join.LeftQualifier, out var leftRow) || leftRow == null)
                    {
                        if (join.Kind == SqlJoinKind.Left)
                        {
                            var nc = new Dictionary<string, RowData?>(ctx, StringComparer.OrdinalIgnoreCase)
                            {
                                [rightQual] = null
                            };
                            next.Add(nc);
                        }

                        continue;
                    }

                    var leftVal = leftRow.GetValue(join.LeftColumn);
                    var matches = new List<RowData>();
                    foreach (var rr in rightRows)
                    {
                        if (StrEq(rr.GetValue(join.RightColumn), leftVal))
                            matches.Add(rr);
                    }

                    if (matches.Count == 0 && join.Kind == SqlJoinKind.Left)
                    {
                        var nc = new Dictionary<string, RowData?>(ctx, StringComparer.OrdinalIgnoreCase)
                        {
                            [rightQual] = null
                        };
                        next.Add(nc);
                    }
                    else
                    {
                        foreach (var m in matches)
                        {
                            var nc = new Dictionary<string, RowData?>(ctx, StringComparer.OrdinalIgnoreCase)
                            {
                                [rightQual] = m
                            };
                            next.Add(nc);
                        }
                    }
                }

                contexts = next;
            }
        }

        HashSet<string>? inSet = null;
        if (ext.WhereIn != null)
        {
            inSet = await ExecuteSubqueryDistinctColumnValuesAsync(
                    databasePath,
                    ext.WhereIn.Subquery,
                    mvccSnapshotCommitted,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        bool PassesFilters(Dictionary<string, RowData?> ctx)
        {
            if (ext.WhereAndChain != null)
            {
                foreach (var (qual, col, val) in ext.WhereAndChain)
                {
                    if (!StrEq(GetCol(ctx, qual, col), val))
                        return false;
                }
            }
            else if (cmd.WhereColumn != null && cmd.WhereValue != null)
            {
                if (!StrEq(GetCol(ctx, null, cmd.WhereColumn), cmd.WhereValue))
                    return false;
            }

            if (ext.WhereIn != null)
            {
                var q = ext.WhereIn.ColumnQualifier;
                var v = GetCol(ctx, q, ext.WhereIn.ColumnName);
                if (v == null || inSet == null || !inSet.Contains(v))
                    return false;
            }

            if (ext.WhereExists != null)
            {
                var ex = ext.WhereExists;
                var outerVal = GetCol(ctx, ex.OuterQualifier, ex.OuterColumn);
                var innerRows = rowCache.TryGetValue(ex.InnerTable, out var ir) ? ir : new List<RowData>();
                var any = false;
                foreach (var irRow in innerRows)
                {
                    if (StrEq(irRow.GetValue(ex.InnerColumn), outerVal))
                    {
                        any = true;
                        break;
                    }
                }

                if (ex.NotExists ? any : !any)
                    return false;
            }

            return true;
        }

        for (var i = contexts.Count - 1; i >= 0; i--)
        {
            if (!PassesFilters(contexts[i]))
                contexts.RemoveAt(i);
        }

        var projections = ext.Projections;
        IReadOnlyList<string> outputNames;
        if (projections is { Count: > 0 })
            outputNames = projections.Select(p => p.OutputName).ToList();
        else if (cmd.ColumnNames is { Count: > 0 } cn)
            outputNames = cn;
        else
        {
            var starTable = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
                ?? throw new SchemaException($"Table '{cmd.TableName}' not found");
            outputNames = starTable.Columns
                .Select(c => c.Name)
                .Where(n => !IsPhase4SystemColumn(n))
                .ToList();
        }

        if (ext.GroupBy is { Count: > 0 } gb)
        {
            string GroupKey(Dictionary<string, RowData?> ctx)
            {
                var parts = new List<string>();
                foreach (var col in gb)
                {
                    var v = GetCol(ctx, null, col);
                    parts.Add(v ?? "\0");
                }

                return string.Join('\u0001', parts);
            }

            var buckets = new Dictionary<string, List<Dictionary<string, RowData?>>>(StringComparer.Ordinal);
            foreach (var ctx in contexts)
            {
                var k = GroupKey(ctx);
                if (!buckets.TryGetValue(k, out var list))
                {
                    list = new List<Dictionary<string, RowData?>>();
                    buckets[k] = list;
                }

                list.Add(ctx);
            }

            var outRows = new List<RowData>();
            foreach (var bucket in buckets.Values)
            {
                if (!PassesHaving(bucket, ext.Having, projections))
                    continue;
                outRows.Add(BuildGroupProjectionRow(bucket, projections, gb, GetCol, bucket[0]));
            }

            if (ext.OrderBy is { Count: > 0 } obg)
                SortResultRows(outRows, obg);

            return new EngineResult(0, new QueryResult(outputNames.ToList(), outRows));
        }

        if (ext.OrderBy is { Count: > 0 } ob)
            contexts.Sort((a, b) => CompareContextOrder(a, b, ob, fromQual, GetCol));

        var projected = new List<RowData>();
        foreach (var ctx in contexts)
            projected.Add(await ProjectRowAsync(ctx, cmd, ext, databasePath, RowsAsync, GetCol, cancellationToken).ConfigureAwait(false));

        return new EngineResult(0, new QueryResult(outputNames.ToList(), projected));
    }

    private static bool IsPhase4SystemColumn(string name) =>
        name.Equals(TableDefinition.RowIdColumnName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(TableDefinition.MvccXminKey, StringComparison.OrdinalIgnoreCase)
        || name.Equals(TableDefinition.MvccXmaxKey, StringComparison.OrdinalIgnoreCase)
        || name.Equals(TableDefinition.VacuumOmitKey, StringComparison.OrdinalIgnoreCase);

    private static int CompareContextOrder(
        IReadOnlyDictionary<string, RowData?> a,
        IReadOnlyDictionary<string, RowData?> b,
        IReadOnlyList<OrderByEntry> order,
        string defaultQual,
        Func<IReadOnlyDictionary<string, RowData?>, string?, string, string?> getCol)
    {
        foreach (var e in order)
        {
            var q = string.IsNullOrEmpty(e.Qualifier) ? defaultQual : e.Qualifier;
            var va = getCol(a, q, e.Column) ?? "";
            var vb = getCol(b, q, e.Column) ?? "";
            var cmp = string.Compare(va, vb, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0)
                return e.Ascending ? cmp : -cmp;
        }

        return 0;
    }

    private static void SortResultRows(List<RowData> rows, IReadOnlyList<OrderByEntry> order)
    {
        rows.Sort((a, b) =>
        {
            foreach (var e in order)
            {
                var va = a.GetValue(e.Column) ?? "";
                var vb = b.GetValue(e.Column) ?? "";
                var cmp = string.Compare(va, vb, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return e.Ascending ? cmp : -cmp;
            }

            return 0;
        });
    }

    private static bool PassesHaving(
        List<Dictionary<string, RowData?>> bucket,
        HavingPredicate? having,
        IReadOnlyList<ProjectionItem>? projections)
    {
        if (having == null)
            return true;
        var val = EvaluateAggregateOnBucket(bucket, having.AggregateKind, having.AggregateColumn, projections);
        return CompareHaving(having.Op, val, having.Literal);
    }

    private static double EvaluateAggregateOnBucket(
        List<Dictionary<string, RowData?>> bucket,
        ProjectionKind kind,
        string? aggregateColumn,
        IReadOnlyList<ProjectionItem>? projections)
    {
        return kind switch
        {
            ProjectionKind.CountStar => bucket.Count,
            ProjectionKind.CountColumn => bucket.Count(r =>
                GetAnyRow(r)!.GetValue(aggregateColumn!) != null),
            ProjectionKind.Sum => bucket.Sum(r => ParseDouble(GetAnyRow(r)!.GetValue(aggregateColumn!))),
            ProjectionKind.Avg => bucket.Count == 0
                ? 0
                : bucket.Sum(r => ParseDouble(GetAnyRow(r)!.GetValue(aggregateColumn!))) / bucket.Count,
            ProjectionKind.Min => bucket.Count == 0
                ? 0
                : bucket.Select(r => ParseDouble(GetAnyRow(r)!.GetValue(aggregateColumn!))).Min(),
            ProjectionKind.Max => bucket.Count == 0
                ? 0
                : bucket.Select(r => ParseDouble(GetAnyRow(r)!.GetValue(aggregateColumn!))).Max(),
            _ => 0
        };
    }

    private static RowData? GetAnyRow(Dictionary<string, RowData?> ctx)
    {
        foreach (var r in ctx.Values)
        {
            if (r != null)
                return r;
        }

        return null;
    }

    private static bool CompareHaving(string op, double actual, string literal)
    {
        if (!double.TryParse(literal, NumberStyles.Any, CultureInfo.InvariantCulture, out var lit))
            return false;
        return op switch
        {
            ">" => actual > lit,
            "<" => actual < lit,
            "=" => Math.Abs(actual - lit) < 1e-9,
            _ => false
        };
    }

    private static RowData BuildGroupProjectionRow(
        List<Dictionary<string, RowData?>> bucket,
        IReadOnlyList<ProjectionItem>? projections,
        IReadOnlyList<string> groupBy,
        Func<IReadOnlyDictionary<string, RowData?>, string?, string, string?> getCol,
        Dictionary<string, RowData?> sample)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (projections == null)
            return new RowData(dict);
        foreach (var p in projections)
        {
            switch (p.Kind)
            {
                case ProjectionKind.Column:
                    if (groupBy.Any(g => g.Equals(p.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    {
                        var v = getCol(sample, p.TableQualifier, p.ColumnName!);
                        if (v != null)
                            dict[p.OutputName] = v;
                    }

                    break;
                default:
                {
                    var aggVal = EvaluateAggregateOnBucket(bucket, p.Kind, p.ColumnName, projections);
                    dict[p.OutputName] = FormatAggregateValue(p.Kind, aggVal, bucket, p.ColumnName);
                    break;
                }
            }
        }

        return new RowData(dict);
    }

    private static string FormatAggregateValue(ProjectionKind kind, double val, List<Dictionary<string, RowData?>> bucket, string? col)
    {
        return kind switch
        {
            ProjectionKind.CountStar or ProjectionKind.CountColumn => ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture),
            ProjectionKind.Sum or ProjectionKind.Min or ProjectionKind.Max => FormatNumber(val),
            ProjectionKind.Avg => FormatNumber(val),
            _ => ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string FormatNumber(double val)
    {
        if (double.IsNaN(val) || double.IsInfinity(val))
            return "0";
        if (Math.Abs(val - Math.Round(val)) < 1e-9)
            return ((long)Math.Round(val)).ToString(CultureInfo.InvariantCulture);
        return val.ToString("G", CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return 0;
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private async Task<RowData> ProjectRowAsync(
        Dictionary<string, RowData?> ctx,
        SelectCommand cmd,
        SelectPhase4Extensions ext,
        string databasePath,
        Func<string, Task<List<RowData>>> rowsAsync,
        Func<IReadOnlyDictionary<string, RowData?>, string?, string, string?> getCol,
        CancellationToken cancellationToken)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ext.Projections is { Count: > 0 } proj)
        {
            foreach (var p in proj)
            {
                switch (p.Kind)
                {
                    case ProjectionKind.Column:
                    {
                        var v = getCol(ctx, p.TableQualifier, p.ColumnName!);
                        if (v != null)
                            dict[p.OutputName] = v;
                        break;
                    }
                    case ProjectionKind.ScalarSubquery when p.ScalarSub is { } ss:
                    {
                        var outerVal = getCol(ctx, ss.OuterQualifier, ss.OuterColumn);
                        var innerRows = await rowsAsync(ss.InnerTable).ConfigureAwait(false);
                        var matched = innerRows.Where(r => string.Equals(r.GetValue(ss.InnerEqColumn), outerVal, StringComparison.OrdinalIgnoreCase)).ToList();
                        var sv = EvaluateScalarAggregate(matched, ss.AggregateKind, ss.AggregateColumn);
                        dict[p.OutputName] = sv;
                        break;
                    }
                    default:
                        dict[p.OutputName] = EvaluateScalarAggregate(
                            ctx.Values.Where(r => r != null).Cast<RowData>().ToList(),
                            p.Kind,
                            p.ColumnName);
                        break;
                }
            }

            return new RowData(dict);
        }

        if (cmd.ColumnNames is { Count: > 0 } cols)
        {
            foreach (var c in cols)
            {
                var v = getCol(ctx, null, c);
                if (v != null)
                    dict[c] = v;
            }

            return new RowData(dict);
        }

        var starTable = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");
        foreach (var col in starTable.Columns)
        {
            if (IsPhase4SystemColumn(col.Name))
                continue;
            var v = getCol(ctx, null, col.Name);
            if (v != null)
                dict[col.Name] = v;
        }

        return new RowData(dict);
    }

    private static string EvaluateScalarAggregate(IReadOnlyList<RowData> rows, ProjectionKind kind, string? col)
    {
        return kind switch
        {
            ProjectionKind.CountStar => rows.Count.ToString(CultureInfo.InvariantCulture),
            ProjectionKind.CountColumn => rows.Count(r => r.GetValue(col!) != null).ToString(CultureInfo.InvariantCulture),
            ProjectionKind.Sum => FormatNumber(rows.Sum(r => ParseDouble(r.GetValue(col!)))),
            ProjectionKind.Avg => rows.Count == 0
                ? "0"
                : FormatNumber(rows.Sum(r => ParseDouble(r.GetValue(col!))) / rows.Count),
            ProjectionKind.Min => rows.Count == 0
                ? ""
                : rows.Select(r => r.GetValue(col!) ?? "").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First(),
            ProjectionKind.Max => rows.Count == 0
                ? ""
                : rows.Select(r => r.GetValue(col!) ?? "").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Last(),
            _ => "0"
        };
    }

    private async Task<HashSet<string>> ExecuteSubqueryDistinctColumnValuesAsync(
        string databasePath,
        SelectCommand sub,
        long? mvccSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        var subResult = await ExecuteSelectAsync(databasePath, sub, mvccSnapshotCommitted, cancellationToken).ConfigureAwait(false);
        var qr = subResult.QueryResult ?? throw new InvalidOperationException("Subquery produced no result");
        if (qr.ColumnNames.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var col = qr.ColumnNames[0];
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in qr.Rows)
        {
            var v = row.GetValue(col);
            if (v != null)
                set.Add(v);
        }

        return set;
    }
}
