using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using ArchC.Messaging.Producers;
using Shared.Models;
using Shared.Tests;

namespace ArchC.Messaging.Tests;

public class ProducerRetryTests
{
    private static async Task<List<Metric>> DrainAsync(ChannelReader<Metric> reader)
    {
        var metrics = new List<Metric>();
        await foreach (var metric in reader.ReadAllAsync())
            metrics.Add(metric);
        return metrics;
    }

    [Fact]
    public async Task Producer_SourceFailsOnceThenRecovers_MetricsStillWrittenToChannel()
    {
        var handler = new FakeHttpHandler(
            TestData.ResponsesFor("OpenMeteo"),
            new Dictionary<string, int> { [TestData.FirstEndpoint("OpenMeteo")] = 1 });

        var producer = new OpenMeteoProducer(TestData.ConfigFor("OpenMeteo"), NullLoggerFactory.Instance, handler);
        var channel = Channel.CreateUnbounded<Metric>();

        await producer.ProduceAsync(channel.Writer);
        channel.Writer.Complete();

        var metrics = await DrainAsync(channel.Reader);

        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, m => Assert.StartsWith("weather.", m.Name));
        // First request failed, retry succeeded
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Producer_SourceFailsAllAttempts_FinishesWithoutExceptionAndWritesNothing()
    {
        var handler = new FakeHttpHandler(
            TestData.ResponsesFor("OpenMeteo"),
            new Dictionary<string, int> { [TestData.FirstEndpoint("OpenMeteo")] = int.MaxValue });

        var producer = new OpenMeteoProducer(TestData.ConfigFor("OpenMeteo"), NullLoggerFactory.Instance, handler);
        var channel = Channel.CreateUnbounded<Metric>();

        await producer.ProduceAsync(channel.Writer);
        channel.Writer.Complete();

        var metrics = await DrainAsync(channel.Reader);

        Assert.Empty(metrics);
        // Exactly three attempts, then controlled give-up
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task FullPipeline_OneSourceRecoversAfterTransientFailure_AllMetricsSent()
    {
        // StackOverflow is transiently down: first request fails, retry succeeds
        var handler = new FakeHttpHandler(
            TestData.AllResponses(),
            new Dictionary<string, int> { [TestData.FirstEndpoint("StackOverflow")] = 1 });

        var (orchestrator, mockTarget) = MessagingPipeline.Build(TestData.Config(), handler);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank + 3 OpenMeteo = 15, nothing lost
        Assert.Equal(TestData.TotalMetrics, mockTarget.SentMetrics.Count);
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("stackoverflow."));
    }
}
