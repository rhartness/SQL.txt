using SqlTxt.Contracts;
using SqlTxt.Engine;

namespace SqlTxt.Service;

/// <summary>
/// Worker for SqlTxt.Service. Phase 2 will implement the full hosted API.
/// When SQLTXT_BUILD_SAMPLE_WIKI is set to a path, builds the sample Wiki database on startup.
/// </summary>
public sealed class Worker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var path = Environment.GetEnvironmentVariable("SQLTXT_BUILD_SAMPLE_WIKI");
        if (!string.IsNullOrWhiteSpace(path))
        {
            var engine = new DatabaseEngine();
            var result = await engine.BuildSampleWikiAsync(path, new BuildSampleWikiOptions(Verbose: false, DeleteIfExists: true), stoppingToken).ConfigureAwait(false);
            // Log or emit result as needed when Phase 2 adds observability
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }
}
