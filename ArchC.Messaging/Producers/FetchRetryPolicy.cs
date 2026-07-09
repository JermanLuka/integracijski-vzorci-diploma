using Microsoft.Extensions.Logging;
using Shared.Models;

namespace ArchC.Messaging.Producers;

/// <summary>
/// Retries a source fetch with a bounded number of attempts and short exponential
/// backoff, so producers can recover from transient source outages. Returns null
/// when all attempts are exhausted; the producer then finishes without writing
/// anything, leaving the rest of the pipeline unaffected.
/// </summary>
internal static class FetchRetryPolicy
{
    private const int MaxAttempts = 3;
    private const int BaseDelayMilliseconds = 50;

    public static async Task<IReadOnlyList<Metric>?> FetchAsync(
        Func<CancellationToken, Task<IReadOnlyList<Metric>>> fetch,
        string producerName,
        ILogger logger,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var metrics = await fetch(ct);

                if (attempt > 1)
                    logger.LogInformation("{Producer} fetch succeeded on attempt {Attempt}/{Max}",
                        producerName, attempt, MaxAttempts);

                return metrics;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogWarning(ex, "{Producer} fetch failed after {Max} attempts, giving up on source",
                        producerName, MaxAttempts);
                    return null;
                }

                var delay = TimeSpan.FromMilliseconds(BaseDelayMilliseconds * Math.Pow(2, attempt - 1));
                logger.LogWarning(ex, "{Producer} fetch attempt {Attempt}/{Max} failed, retrying in {Delay}ms",
                    producerName, attempt, MaxAttempts, delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
        }

        return null;
    }
}
