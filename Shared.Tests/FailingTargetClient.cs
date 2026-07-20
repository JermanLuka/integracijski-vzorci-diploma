using System.Collections.Concurrent;
using Shared.Models;
using Shared.Target;

namespace Shared.Tests;

public sealed class FailingTargetClient : ITargetClient
{
    private readonly int _failuresBeforeSuccess;
    private readonly ConcurrentBag<Metric> _sentMetrics = [];
    private int _sendCount;

    public FailingTargetClient(int failuresBeforeSuccess)
    {
        _failuresBeforeSuccess = failuresBeforeSuccess;
    }

    public int SendCount => _sendCount;

    public IReadOnlyCollection<Metric> SentMetrics => _sentMetrics;

    public Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default)
    {
        var attempt = Interlocked.Increment(ref _sendCount);

        if (attempt <= _failuresBeforeSuccess)
            return Task.FromResult(new SendResult(false, 0, $"Simulated target failure (attempt {attempt})"));

        var list = metrics.ToList();
        foreach (var m in list)
            _sentMetrics.Add(m);

        return Task.FromResult(new SendResult(true, list.Count));
    }
}
