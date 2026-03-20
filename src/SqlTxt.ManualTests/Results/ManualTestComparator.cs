namespace SqlTxt.ManualTests.Results;

/// <summary>
/// Named baseline storage for deficit reporting (e.g. LocalDB). Add more entries when additional comparators exist.
/// </summary>
public readonly record struct ManualTestComparator(string DisplayName, string StorageLabel)
{
    /// <summary>Default comparators for the manual suite.</summary>
    public static IReadOnlyList<ManualTestComparator> DefaultSuite { get; } =
    [
        new("SQL Server LocalDB", "localdb")
    ];
}
