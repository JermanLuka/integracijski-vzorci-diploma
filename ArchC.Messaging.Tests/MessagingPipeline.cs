using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ArchC.Messaging.Producers;
using Shared.Target;
using Shared.Tests;

namespace ArchC.Messaging.Tests;

/// <summary>
/// Builds a ready-to-run messaging pipeline for tests: the four producers, the
/// consumer, and the orchestrator, all wired to fake HTTP responses and an
/// in-memory target instead of real services.
/// </summary>
internal static class MessagingPipeline
{
    public static (MessagingOrchestrator Orchestrator, MockTargetClient Target) Build(
        IConfiguration config, FakeHttpHandler handler)
    {
        var loggerFactory = NullLoggerFactory.Instance;

        var github = new GitHubProducer(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowProducer(config, loggerFactory, handler);
        var worldbank = new WorldBankProducer(config, loggerFactory, handler);
        var openmeteo = new OpenMeteoProducer(config, loggerFactory, handler);

        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new MetricConsumer(target, NullLogger<MetricConsumer>.Instance);

        var orchestrator = new MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<MessagingOrchestrator>.Instance);

        return (orchestrator, target);
    }
}
