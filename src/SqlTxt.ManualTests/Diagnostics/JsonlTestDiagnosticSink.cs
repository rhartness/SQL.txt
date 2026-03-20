using System.Text.Json;

namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Appends UTF-8 JSON Lines to a file (one object per line).
/// </summary>
public sealed class JsonlTestDiagnosticSink : ITestDiagnosticSink
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _path;
    private readonly object _lock = new();
    private StreamWriter? _writer;

    public JsonlTestDiagnosticSink(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public void Write(DiagnosticEventDto e)
    {
        lock (_lock)
        {
            if (_writer is null) return;
            _writer.WriteLine(JsonSerializer.Serialize(e, JsonOpts));
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
