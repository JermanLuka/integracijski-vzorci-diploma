using Microsoft.Extensions.Logging.Abstractions;
using Shared.Target;
using Shared.Tests;

namespace ArchB.Monolith.Tests;

public class MetricOrchestratorTests
{
    [Fact]
    public async Task RunAsync_CollectsAndSendsAllMetrics()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesFor("GitHub", "StackOverflow", "WorldBank"));
        var config = TestData.ConfigFor("GitHub", "StackOverflow", "WorldBank");
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler);
        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, mockTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank = 12 metrics
        Assert.Equal(12, mockTarget.SentMetrics.Count);
        Assert.Equal(1, mockTarget.SendCount);
    }

    [Fact]
    public async Task DataAccess_SkipsSourcesWithMissingTokens()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesFor("WorldBank"));
        var dataAccess = new DataAccess(TestData.ConfigFor("WorldBank"), NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Empty(github);
        Assert.Empty(stackoverflow);
        Assert.Equal(3, worldbank.Count);
        Assert.All(worldbank, m => Assert.StartsWith("worldbank.", m.Name));
    }

    [Fact]
    public async Task DataAccess_ReturnsEmptyWhenSourceFails()
    {
        // Handler with no canned responses -all HTTP calls return 404
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = TestData.ConfigFor("GitHub", "StackOverflow", "WorldBank");
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Empty(github);
        Assert.Empty(stackoverflow);
        Assert.Empty(worldbank);
    }

    [Fact]
    public async Task RunAsync_ReturnsFalseWhenNoMetricsCollected()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var dataAccess = new DataAccess(TestData.ConfigFor("WorldBank"), NullLoggerFactory.Instance, handler);
        var mockTarget = new MockTargetClient(NullLogger<MockTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, mockTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
        Assert.Equal(0, mockTarget.SendCount);
    }

    [Fact]
    public async Task RunAsync_ReturnsFalseWhenTargetClientFails()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesFor("WorldBank"));
        var dataAccess = new DataAccess(TestData.ConfigFor("WorldBank"), NullLoggerFactory.Instance, handler);
        var failingTarget = new RealTargetClient(
            new HttpClient(), "http://localhost:0/invalid", NullLogger<RealTargetClient>.Instance);
        var orchestrator = new MetricOrchestrator(dataAccess, failingTarget, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
    }

    [Fact]
    public async Task DataAccess_PartialFailureDoesNotAffectOtherSources()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesFor("GitHub", "WorldBank"));
        var config = TestData.ConfigFor("GitHub", "StackOverflow", "WorldBank");
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler);

        var github = await dataAccess.FetchGitHubAsync();
        var stackoverflow = await dataAccess.FetchStackOverflowAsync();
        var worldbank = await dataAccess.FetchWorldBankAsync();

        Assert.Equal(5, github.Count);
        Assert.Empty(stackoverflow); // failed gracefully
        Assert.Equal(3, worldbank.Count);
    }

    [Fact]
    public async Task DataAccess_FailFast_PropagatesException()
    {
        // No canned responses -all HTTP calls will fail
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = TestData.ConfigFor("GitHub", "StackOverflow", "WorldBank");
        var dataAccess = new DataAccess(config, NullLoggerFactory.Instance, handler, failFast: true);

        await Assert.ThrowsAsync<HttpRequestException>(() => dataAccess.FetchGitHubAsync());
    }
}
