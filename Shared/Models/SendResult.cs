namespace Shared.Models;

/// <summary>
/// Result of sending metrics to a target system.
/// </summary>
public sealed record SendResult(
    bool Success,
    int MetricsSent,
    string? ErrorMessage = null);
