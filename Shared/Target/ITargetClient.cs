using Shared.Models;

namespace Shared.Target;

/// <summary>
/// Sends collected metrics to a target system.
/// </summary>
public interface ITargetClient
{
    Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default);
}
