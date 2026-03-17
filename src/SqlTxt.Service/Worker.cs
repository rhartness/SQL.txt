namespace SqlTxt.Service;

/// <summary>
/// Placeholder worker for SqlTxt.Service. Phase 2 will implement the hosted API.
/// </summary>
public sealed class Worker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
