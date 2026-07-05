using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Target;

/// <summary>
/// Sends metrics as JSON via HTTP POST to a configurable target URL.
/// Used for demo purposes only.
/// </summary>
public sealed class RealTargetClient : ITargetClient
{
    private readonly HttpClient _http;
    private readonly string _targetUrl;
    private readonly ILogger<RealTargetClient> _logger;

    public RealTargetClient(HttpClient http, string targetUrl, ILogger<RealTargetClient> logger)
    {
        _http = http;
        _targetUrl = targetUrl;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(IEnumerable<Metric> metrics, CancellationToken ct = default)
    {
        var list = metrics.ToList();

        try
        {
            var response = await _http.PostAsJsonAsync(_targetUrl, list, ct);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation(
                "RealTargetClient sent {Count} metrics to {Url} -{Status}",
                list.Count, _targetUrl, response.StatusCode);

            return new SendResult(true, list.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RealTargetClient failed to send {Count} metrics to {Url}",
                list.Count, _targetUrl);

            return new SendResult(false, 0, ex.Message);
        }
    }
}
