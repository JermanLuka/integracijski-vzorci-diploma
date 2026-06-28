using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ArchA.Hexagonal.Adapters;
using ArchA.Hexagonal.Core;
using Shared.Target;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

// Register source adapters (input ports)
builder.Services.AddSingleton<IMetricSource, GitHubAdapter>();
builder.Services.AddSingleton<IMetricSource, StackOverflowAdapter>();
builder.Services.AddSingleton<IMetricSource, WorldBankAdapter>();

// Register target client and sink adapter (output port)
var useMock = builder.Configuration.GetValue<bool>("Target:UseMock");
if (useMock)
{
    builder.Services.AddSingleton<ITargetClient, MockTargetClient>();
}
else
{
    builder.Services.AddSingleton<ITargetClient>(sp =>
        new RealTargetClient(
            new HttpClient(),
            builder.Configuration["Target:RealUrl"]!,
            sp.GetRequiredService<ILogger<RealTargetClient>>()));
}
builder.Services.AddSingleton<IMetricSink, TargetSinkAdapter>();

// Register orchestrator
builder.Services.AddSingleton<MetricOrchestrator>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Using {Target} target client", useMock ? "Mock" : "Real");

var orchestrator = host.Services.GetRequiredService<MetricOrchestrator>();
var success = await orchestrator.RunAsync();

return success ? 0 : 1;
