using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models;
using Shared.Target;

namespace Shared.Tests;

public class MockTargetClientTests
{
    private readonly MockTargetClient _client = new(NullLogger<MockTargetClient>.Instance);

    [Fact]
    public async Task SendAsync_StoresMetrics()
    {
        var metrics = new[]
        {
            new Metric("a", 1, DateTimeOffset.UtcNow),
            new Metric("b", 2, DateTimeOffset.UtcNow),
        };

        var result = await _client.SendAsync(metrics);

        Assert.True(result.Success);
        Assert.Equal(2, result.MetricsSent);
        Assert.Equal(2, _client.SentMetrics.Count);
    }

    [Fact]
    public async Task SendAsync_AccumulatesAcrossCalls()
    {
        await _client.SendAsync([new Metric("a", 1, DateTimeOffset.UtcNow)]);
        await _client.SendAsync([new Metric("b", 2, DateTimeOffset.UtcNow)]);

        Assert.Equal(2, _client.SendCount);
        Assert.Equal(2, _client.SentMetrics.Count);
    }

    [Fact]
    public async Task SendAsync_EmptyCollection_ReturnsZero()
    {
        var result = await _client.SendAsync([]);

        Assert.True(result.Success);
        Assert.Equal(0, result.MetricsSent);
    }
}
