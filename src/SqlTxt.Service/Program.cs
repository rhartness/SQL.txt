using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlTxt.Service;

// Phase 2: SqlTxt.Service — installable Windows Service / systemd / launchd.
// Placeholder until Phase 2 implementation.
await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build()
    .RunAsync();
