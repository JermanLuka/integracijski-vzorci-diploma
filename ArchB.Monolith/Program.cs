using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Target;
using ArchB.Monolith;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

builder.Services.AddSingleton<DataAccess>();
builder.Services.AddSingleton<MetricOrchestrator>();

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

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Using {Target} target client", useMock ? "Mock" : "Real");

var orchestrator = host.Services.GetRequiredService<MetricOrchestrator>();
var success = await orchestrator.RunAsync();

return success ? 0 : 1;
