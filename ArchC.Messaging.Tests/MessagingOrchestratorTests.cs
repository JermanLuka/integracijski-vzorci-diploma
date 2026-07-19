using Shared.Tests;

namespace ArchC.Messaging.Tests;

public class MessagingOrchestratorTests
{
    [Fact]
    public async Task FullPipeline_AllSourcesSucceed_AllMetricsSent()
    {
        var handler = new FakeHttpHandler(TestData.AllResponses());
        var (orchestrator, mockTarget) = MessagingPipeline.Build(TestData.Config(), handler);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank + 3 OpenMeteo = 15
        Assert.Equal(TestData.TotalMetrics, mockTarget.SentMetrics.Count);
    }

    [Fact]
    public async Task FaultTolerance_OneSourceFails_OthersContinue()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesExcept("StackOverflow"));
        var (orchestrator, mockTarget) = MessagingPipeline.Build(TestData.Config(), handler);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 0 StackOverflow (failed) + 3 WorldBank + 3 OpenMeteo = 11
        Assert.Equal(11, mockTarget.SentMetrics.Count);
        Assert.DoesNotContain(mockTarget.SentMetrics, m => m.Name.StartsWith("stackoverflow."));
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("github."));
        Assert.Contains(mockTarget.SentMetrics, m => m.Name.StartsWith("worldbank."));
    }

    [Fact]
    public async Task GracefulShutdown_ChannelDrainsAndExits()
    {
        // Only WorldBank and OpenMeteo (no tokens for GitHub/StackOverflow)
        var handler = new FakeHttpHandler(TestData.ResponsesFor("WorldBank", "OpenMeteo"));
        var (orchestrator, mockTarget) = MessagingPipeline.Build(
            TestData.ConfigFor("WorldBank", "OpenMeteo"), handler);

        // Should complete without hanging
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var success = await orchestrator.RunAsync(cts.Token);

        Assert.True(success);
        // 3 WorldBank + 3 OpenMeteo = 6
        Assert.Equal(6, mockTarget.SentMetrics.Count);
    }

    [Fact]
    public async Task NoMetrics_ReturnsFalse()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var (orchestrator, mockTarget) = MessagingPipeline.Build(
            TestData.ConfigFor("WorldBank", "OpenMeteo"), handler);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
        Assert.Equal(0, mockTarget.SendCount);
    }
}
