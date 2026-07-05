using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ArchC.Messaging;
using ArchC.Messaging.Producers;
using Shared.Target;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);

// Register producers
builder.Services.AddSingleton<GitHubProducer>();
builder.Services.AddSingleton<StackOverflowProducer>();
builder.Services.AddSingleton<WorldBankProducer>();
builder.Services.AddSingleton<OpenMeteoProducer>();

// Register target client
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

// Register consumer and orchestrator
builder.Services.AddSingleton<MetricConsumer>();
builder.Services.AddSingleton<MessagingOrchestrator>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Using {Target} target client", useMock ? "Mock" : "Real");

var orchestrator = host.Services.GetRequiredService<MessagingOrchestrator>();
var success = await orchestrator.RunAsync();

return success ? 0 : 1;
