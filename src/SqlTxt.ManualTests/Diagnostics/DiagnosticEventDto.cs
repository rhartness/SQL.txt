namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// One JSON Lines record for manual test / harness diagnostics (schema v1).
/// </summary>
public sealed class DiagnosticEventDto
{
    public int v { get; set; } = 1;
    public string runId { get; set; } = "";
    public string tsUtc { get; set; } = "";
    public string? kind { get; set; }

    public string? testName { get; set; }
    public string? storageType { get; set; }
    public string? stage { get; set; }
    public string? step { get; set; }
    public double? elapsedMs { get; set; }
    public Dictionary<string, string>? data { get; set; }
}
