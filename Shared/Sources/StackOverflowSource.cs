using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Sources;

/// <summary>
/// Fetches developer metrics from the Stack Exchange API v2.3.
/// Requires an access token and API key.
/// </summary>
public sealed class StackOverflowSource
{
    private readonly HttpClient _http;
    private readonly string _accessToken;
    private readonly string _apiKey;
    private readonly ILogger<StackOverflowSource> _logger;

    public StackOverflowSource(HttpClient http, string accessToken, string apiKey, ILogger<StackOverflowSource> logger)
    {
        _http = http;
        _accessToken = accessToken;
        _apiKey = apiKey;
        _logger = logger;

        _http.BaseAddress ??= new Uri("https://api.stackexchange.com");
    }

    public async Task<IReadOnlyList<Metric>> FetchMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, string> { ["source"] = "stackoverflow" };

        try
        {
            var url = $"/2.3/me?site=stackoverflow&key={_apiKey}&access_token={_accessToken}&filter=default";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            var items = doc.RootElement.GetProperty("items");
            if (items.GetArrayLength() == 0)
                throw new InvalidOperationException("Stack Exchange API returned no user data.");

            var user = items[0];
            var badgeCounts = user.GetProperty("badge_counts");

            var metrics = new List<Metric>
            {
                new("stackoverflow.reputation", user.GetProperty("reputation").GetInt32(), now, tags),
                new("stackoverflow.gold_badges", badgeCounts.GetProperty("gold").GetInt32(), now, tags),
                new("stackoverflow.silver_badges", badgeCounts.GetProperty("silver").GetInt32(), now, tags),
                new("stackoverflow.bronze_badges", badgeCounts.GetProperty("bronze").GetInt32(), now, tags),
            };

            _logger.LogInformation("StackOverflowSource fetched {Count} metrics", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StackOverflowSource failed to fetch metrics");
            throw;
        }
    }
}
