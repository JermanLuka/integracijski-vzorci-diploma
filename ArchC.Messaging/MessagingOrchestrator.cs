using System.Threading.Channels;
using ArchC.Messaging.Producers;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace ArchC.Messaging;

public sealed class MessagingOrchestrator
{
    private const int ChannelCapacity = 100;
    private const int ConsumerCount = 2;

    private readonly GitHubProducer _github;
    private readonly StackOverflowProducer _stackoverflow;
    private readonly WorldBankProducer _worldbank;
    private readonly MetricConsumer _consumer;
    private readonly ILogger<MessagingOrchestrator> _logger;

    public MessagingOrchestrator(
        GitHubProducer github,
        StackOverflowProducer stackoverflow,
        WorldBankProducer worldbank,
        MetricConsumer consumer,
        ILogger<MessagingOrchestrator> logger)
    {
        _github = github;
        _stackoverflow = stackoverflow;
        _worldbank = worldbank;
        _consumer = consumer;
        _logger = logger;
    }

    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Orchestrator starting (channel capacity: {Capacity}, consumers: {Consumers})",
            ChannelCapacity, ConsumerCount);

        var channel = Channel.CreateBounded<Metric>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false,
        });

        // Start multiple consumers concurrently
        var consumerTasks = Enumerable.Range(0, ConsumerCount)
            .Select(_ => _consumer.ConsumeAsync(channel.Reader, ct))
            .ToArray();

        // Start all producers concurrently
        var producerTasks = new[]
        {
            _github.ProduceAsync(channel.Writer, ct),
            _stackoverflow.ProduceAsync(channel.Writer, ct),
            _worldbank.ProduceAsync(channel.Writer, ct),
        };

        await Task.WhenAll(producerTasks);

        // Signal that no more metrics will be written
        channel.Writer.Complete();
        _logger.LogInformation("All producers finished, channel completed");

        // Wait for all consumers to drain the channel
        var results = await Task.WhenAll(consumerTasks);

        var totalSent = results.Sum(r => r.Sent);
        var totalFailed = results.Sum(r => r.Failed);
        var totalDeadLettered = results.Sum(r => r.DeadLettered);

        _logger.LogInformation(
            "Orchestrator finished. Sent: {Sent}, Failed: {Failed}, Dead-lettered: {DeadLettered}",
            totalSent, totalFailed, totalDeadLettered);

        return totalSent > 0;
    }
}
