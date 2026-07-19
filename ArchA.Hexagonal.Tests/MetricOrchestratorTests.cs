using Microsoft.Extensions.Logging.Abstractions;
using ArchA.Hexagonal.Adapters;
using ArchA.Hexagonal.Core;
using Shared.Models;
using Shared.Tests;

namespace ArchA.Hexagonal.Tests;

internal sealed class FakeMetricSource : IMetricSource
{
    private readonly IReadOnlyList<Metric> _metrics;

    public FakeMetricSource(string name, IReadOnlyList<Metric> metrics)
    {
        Name = name;
        _metrics = metrics;
    }

    public string Name { get; }

    public Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
        => Task.FromResult(_metrics);
}

internal sealed class FailingMetricSource : IMetricSource
{
    public string Name => "Failing";

    public Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated source failure");
}

internal sealed class FakeMetricSink : IMetricSink
{
    public List<Metric> Received { get; } = [];
    public int SendCount { get; private set; }

    public Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default)
    {
        var list = metrics.ToList();
        Received.AddRange(list);
        SendCount++;
        return Task.FromResult(new SendResult(true, list.Count));
    }
}

public class MetricOrchestratorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Core_RunAsync_CollectsFromAllSourcesAndSends()
    {
        var source1 = new FakeMetricSource("A", [new Metric("a.value", 1, Now)]);
        var source2 = new FakeMetricSource("B", [new Metric("b.value", 2, Now), new Metric("b.other", 3, Now)]);
        var sink = new FakeMetricSink();
        var orchestrator = new MetricOrchestrator([source1, source2], sink, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        Assert.Equal(3, sink.Received.Count);
        Assert.Equal(1, sink.SendCount);
    }

    [Fact]
    public async Task Core_RunAsync_ReturnsFalseWhenNoMetrics()
    {
        var emptySource = new FakeMetricSource("Empty", []);
        var sink = new FakeMetricSink();
        var orchestrator = new MetricOrchestrator([emptySource], sink, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.False(success);
        Assert.Equal(0, sink.SendCount);
    }

    [Fact]
    public async Task Core_RunAsync_ContinuesWhenOneSourceFails()
    {
        var good = new FakeMetricSource("Good", [new Metric("good.value", 42, Now)]);
        var failing = new FailingMetricSource();
        var sink = new FakeMetricSink();
        var orchestrator = new MetricOrchestrator([good, failing], sink, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        Assert.Single(sink.Received);
        Assert.Equal("good.value", sink.Received[0].Name);
    }

    [Fact]
    public async Task Adapters_FetchThroughSharedSources()
    {
        var handler = new FakeHttpHandler(TestData.ResponsesFor("GitHub", "StackOverflow", "WorldBank"));
        var config = TestData.ConfigFor("GitHub", "StackOverflow", "WorldBank");
        var loggerFactory = NullLoggerFactory.Instance;

        IMetricSource[] sources =
        [
            new GitHubAdapter(config, loggerFactory, handler),
            new StackOverflowAdapter(config, loggerFactory, handler),
            new WorldBankAdapter(config, loggerFactory, handler),
        ];

        var sink = new FakeMetricSink();
        var orchestrator = new MetricOrchestrator(sources, sink, NullLogger<MetricOrchestrator>.Instance);

        var success = await orchestrator.RunAsync();

        Assert.True(success);
        // 5 GitHub + 4 StackOverflow + 3 WorldBank = 12
        Assert.Equal(12, sink.Received.Count);
    }

    [Fact]
    public async Task Adapters_SkipWhenTokensMissing()
    {
        var handler = new FakeHttpHandler(new Dictionary<string, string>());
        var config = TestData.ConfigFor("WorldBank");
        var loggerFactory = NullLoggerFactory.Instance;
        var github = new GitHubAdapter(config, loggerFactory, handler);
        var stackoverflow = new StackOverflowAdapter(config, loggerFactory, handler);

        var ghMetrics = await github.FetchAsync();
        var soMetrics = await stackoverflow.FetchAsync();

        Assert.Empty(ghMetrics);
        Assert.Empty(soMetrics);
    }
}
