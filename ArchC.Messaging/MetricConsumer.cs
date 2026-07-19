using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Target;

namespace ArchC.Messaging;

public sealed class MetricConsumer
{
    private const int MaxRetries = 3;
    private const int BatchSize = 5;
    private static readonly TimeSpan BatchTimeout = TimeSpan.FromMilliseconds(500);

    private readonly ITargetClient _target;
    private readonly ILogger<MetricConsumer> _logger;
    private readonly ConcurrentBag<Metric> _deadLetterQueue = [];

    public IReadOnlyCollection<Metric> DeadLetters => _deadLetterQueue;

    public MetricConsumer(ITargetClient target, ILogger<MetricConsumer> logger)
    {
        _target = target;
        _logger = logger;
    }

    public async Task<ConsumeResult> ConsumeAsync(ChannelReader<Metric> reader, CancellationToken ct = default)
    {
        int totalSent = 0;
        int totalFailed = 0;
        var batch = new List<Metric>();

        await foreach (var metric in reader.ReadAllAsync(ct))
        {
            batch.Add(metric);

            if (batch.Count >= BatchSize)
            {
                var (sent, failed) = await SendBatchWithRetryAsync(batch, ct);
                totalSent += sent;
                totalFailed += failed;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            var (sent, failed) = await SendBatchWithRetryAsync(batch, ct);
            totalSent += sent;
            totalFailed += failed;
        }

        if (totalSent == 0 && totalFailed == 0)
            _logger.LogWarning("Consumer received no metrics from channel");

        return new ConsumeResult(totalSent, totalFailed, _deadLetterQueue.Count);
    }

    private async Task<(int Sent, int Failed)> SendBatchWithRetryAsync(List<Metric> batch, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var result = await _target.SendAsync(batch, ct);

            if (result.Success)
            {
                _logger.LogInformation("Consumer sent {Count} metrics on attempt {Attempt}", result.MetricsSent, attempt);
                return (result.MetricsSent, 0);
            }

            var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
            _logger.LogWarning("Send attempt {Attempt}/{Max} failed: {Error}. Retrying in {Delay}ms",
                attempt, MaxRetries, result.ErrorMessage, delay.TotalMilliseconds);

            await Task.Delay(delay, ct);
        }

        foreach (var metric in batch)
            _deadLetterQueue.Add(metric);

        _logger.LogError("Dead-lettered {Count} metrics after {Max} failed attempts", batch.Count, MaxRetries);
        return (0, batch.Count);
    }
}
