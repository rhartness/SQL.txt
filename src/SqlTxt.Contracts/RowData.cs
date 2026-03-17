namespace SqlTxt.Contracts;

/// <summary>
/// Represents a single row of data as column name to string value mapping.
/// </summary>
public sealed record RowData(IReadOnlyDictionary<string, string> Values)
{
    /// <summary>
    /// Gets the value for a column by name, or null if not present.
    /// </summary>
    public string? GetValue(string columnName) =>
        Values.TryGetValue(columnName, out var v) ? v : null;

    /// <summary>
    /// Gets values in column order.
    /// </summary>
    public IReadOnlyList<string> GetValuesInOrder(IEnumerable<string> columnNames) =>
        columnNames.Select(c => GetValue(c) ?? string.Empty).ToList();
}
