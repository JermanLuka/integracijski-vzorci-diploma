using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Target;

/// <summary>
/// Deterministic in-memory target client used for all measurements.
/// No network I/O -simply stores received metrics.
/// </summary>
public sealed class MockTargetClient : ITargetClient
{
    private readonly ConcurrentBag<Metric> _sentMetrics = [];
    private int _sendCount;
    private readonly ILogger<MockTargetClient> _logger;

    public MockTargetClient(ILogger<MockTargetClient> logger)
    {
        _logger = logger;
    }

    /// <summary>All metrics that have been sent through this client.</summary>
    public IReadOnlyCollection<Metric> SentMetrics => _sentMetrics;

    /// <summary>Number of SendAsync calls made.</summary>
    public int SendCount => _sendCount;

    public Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default)
    {
        var list = metrics.ToList();
        foreach (var m in list)
            _sentMetrics.Add(m);

        Interlocked.Increment(ref _sendCount);

        _logger.LogInformation(
            "MockTargetClient received {Count} metrics (total sends: {SendCount})",
            list.Count, _sendCount);

        return Task.FromResult(new SendResult(true, list.Count));
    }
}
