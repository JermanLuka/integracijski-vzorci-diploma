using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Sources;

/// <summary>
/// Fetches weather metrics from the Open Meteo API.
/// No authentication required.
/// </summary>
public sealed class OpenMeteoSource
{
    private readonly HttpClient _http;
    private readonly string _latitude;
    private readonly string _longitude;
    private readonly ILogger<OpenMeteoSource> _logger;

    public OpenMeteoSource(HttpClient http, string latitude, string longitude, ILogger<OpenMeteoSource> logger)
    {
        _http = http;
        _latitude = latitude;
        _longitude = longitude;
        _logger = logger;

        _http.BaseAddress ??= new Uri("https://api.open-meteo.com");
    }

    public async Task<IReadOnlyList<Metric>> FetchMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, string>
        {
            ["source"] = "openmeteo",
            ["latitude"] = _latitude,
            ["longitude"] = _longitude,
        };

        try
        {
            var url = $"/v1/forecast?latitude={_latitude}&longitude={_longitude}&current=temperature_2m,relative_humidity_2m,wind_speed_10m";
            var json = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);

            var current = doc.RootElement.GetProperty("current");
            var metrics = new List<Metric>
            {
                new("weather.temperature", current.GetProperty("temperature_2m").GetDouble(), now, tags),
                new("weather.humidity", current.GetProperty("relative_humidity_2m").GetDouble(), now, tags),
                new("weather.wind_speed", current.GetProperty("wind_speed_10m").GetDouble(), now, tags),
            };

            _logger.LogInformation("OpenMeteoSource fetched {Count} metrics", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenMeteoSource failed to fetch metrics");
            throw;
        }
    }
}
