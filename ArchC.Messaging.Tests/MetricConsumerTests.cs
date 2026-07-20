using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models;
using Shared.Tests;

namespace ArchC.Messaging.Tests;

public class MetricConsumerTests
{
    private static ChannelReader<Metric> ChannelWithMetrics(int count)
    {
        var channel = Channel.CreateUnbounded<Metric>();
        for (int i = 0; i < count; i++)
            channel.Writer.TryWrite(new Metric($"test.metric_{i}", i, DateTimeOffset.UtcNow));
        channel.Writer.Complete();
        return channel.Reader;
    }

    [Fact]
    public async Task TargetFailsOnceThenRecovers_AllMetricsSentAfterRetry()
    {
        var target = new FailingTargetClient(failuresBeforeSuccess: 1);
        var consumer = new MetricConsumer(target, NullLogger<MetricConsumer>.Instance);

        var result = await consumer.ConsumeAsync(ChannelWithMetrics(5));

        Assert.Equal(5, result.Sent);
        Assert.Equal(0, result.DeadLettered);
        Assert.Equal(2, target.SendCount);
        Assert.Equal(5, target.SentMetrics.Count);
        Assert.Empty(consumer.DeadLetters);
    }

    [Fact]
    public async Task TargetAlwaysFails_BatchDeadLetteredAfterMaxRetries()
    {
        var target = new FailingTargetClient(failuresBeforeSuccess: int.MaxValue);
        var consumer = new MetricConsumer(target, NullLogger<MetricConsumer>.Instance);

        var result = await consumer.ConsumeAsync(ChannelWithMetrics(5));

        Assert.Equal(0, result.Sent);
        Assert.Equal(5, result.DeadLettered);
        Assert.Equal(3, target.SendCount);
        Assert.Empty(target.SentMetrics);
        Assert.Equal(5, consumer.DeadLetters.Count);
    }

    [Fact]
    public async Task TwoConsumersSharedInstance_DeadLetterTotalsAreNotDoubleCounted()
    {
        var target = new FailingTargetClient(failuresBeforeSuccess: int.MaxValue);
        var consumer = new MetricConsumer(target, NullLogger<MetricConsumer>.Instance);

        var channel = Channel.CreateUnbounded<Metric>();
        for (int i = 0; i < 10; i++)
            channel.Writer.TryWrite(new Metric($"test.metric_{i}", i, DateTimeOffset.UtcNow));
        channel.Writer.Complete();

        var results = await Task.WhenAll(
            consumer.ConsumeAsync(channel.Reader),
            consumer.ConsumeAsync(channel.Reader));

        Assert.Equal(0, results.Sum(r => r.Sent));
        Assert.Equal(10, results.Sum(r => r.DeadLettered));
        Assert.Equal(10, consumer.DeadLetters.Count);
    }

    [Fact]
    public async Task TargetFailsForFirstBatchOnly_RemainingBatchesStillDelivered()
    {
        var target = new FailingTargetClient(failuresBeforeSuccess: 3);
        var consumer = new MetricConsumer(target, NullLogger<MetricConsumer>.Instance);

        var result = await consumer.ConsumeAsync(ChannelWithMetrics(10));

        Assert.Equal(5, result.Sent);
        Assert.Equal(5, result.DeadLettered);
        Assert.Equal(5, consumer.DeadLetters.Count);
    }
}
