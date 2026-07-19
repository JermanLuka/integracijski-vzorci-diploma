using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Target;

namespace Measurements;

/// <summary>
/// Assembles each architecture's pipeline against a fake HTTP handler and a
/// mock target, exactly as the runners measure them.
/// </summary>
internal static class ArchitectureFactory
{
    public static (ArchB.Monolith.MetricOrchestrator Orchestrator, MockTargetClient Target) BuildMonolith(
        IConfiguration config, HttpMessageHandler handler, bool failFast = false)
    {
        var dataAccess = new ArchB.Monolith.DataAccess(config, NullLoggerFactory.Instance, handler, failFast: failFast);
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new ArchB.Monolith.MetricOrchestrator(
            dataAccess, target, NullLogger<ArchB.Monolith.MetricOrchestrator>.Instance);
        return (orchestrator, target);
    }

    public static (ArchA.Hexagonal.Core.MetricOrchestrator Orchestrator, MockTargetClient Target) BuildHexagonal(
        IConfiguration config, HttpMessageHandler handler)
    {
        var sources = new ArchA.Hexagonal.Core.IMetricSource[]
        {
            new ArchA.Hexagonal.Adapters.GitHubAdapter(config, NullLoggerFactory.Instance, handler),
            new ArchA.Hexagonal.Adapters.StackOverflowAdapter(config, NullLoggerFactory.Instance, handler),
            new ArchA.Hexagonal.Adapters.WorldBankAdapter(config, NullLoggerFactory.Instance, handler),
            new ArchA.Hexagonal.Adapters.OpenMeteoAdapter(config, NullLoggerFactory.Instance, handler),
        };
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var sink = new ArchA.Hexagonal.Adapters.TargetSinkAdapter(target);
        var orchestrator = new ArchA.Hexagonal.Core.MetricOrchestrator(
            sources, sink, NullLogger<ArchA.Hexagonal.Core.MetricOrchestrator>.Instance);
        return (orchestrator, target);
    }

    public static (ArchC.Messaging.MessagingOrchestrator Orchestrator, MockTargetClient Target) BuildMessaging(
        IConfiguration config, HttpMessageHandler handler)
    {
        var github = new ArchC.Messaging.Producers.GitHubProducer(config, NullLoggerFactory.Instance, handler);
        var stackoverflow = new ArchC.Messaging.Producers.StackOverflowProducer(config, NullLoggerFactory.Instance, handler);
        var worldbank = new ArchC.Messaging.Producers.WorldBankProducer(config, NullLoggerFactory.Instance, handler);
        var openmeteo = new ArchC.Messaging.Producers.OpenMeteoProducer(config, NullLoggerFactory.Instance, handler);
        var target = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var consumer = new ArchC.Messaging.MetricConsumer(target, NullLogger<ArchC.Messaging.MetricConsumer>.Instance);
        var orchestrator = new ArchC.Messaging.MessagingOrchestrator(
            github, stackoverflow, worldbank, openmeteo, consumer,
            NullLogger<ArchC.Messaging.MessagingOrchestrator>.Instance);
        return (orchestrator, target);
    }
}
