using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Sources;

/// <summary>
/// Fetches public indicators from the World Bank API.
/// No authentication required.
/// </summary>
public sealed class PublicDataSource
{
    private static readonly (string Indicator, string MetricName)[] Indicators =
    [
        ("NY.GDP.MKTP.CD", "worldbank.gdp_usd"),
        ("SP.POP.TOTL", "worldbank.population"),
        ("SP.DYN.LE00.IN", "worldbank.life_expectancy"),
    ];

    private readonly HttpClient _http;
    private readonly string _countryCode;
    private readonly ILogger<PublicDataSource> _logger;

    public PublicDataSource(HttpClient http, string countryCode, ILogger<PublicDataSource> logger)
    {
        _http = http;
        _countryCode = countryCode;
        _logger = logger;

        _http.BaseAddress ??= new Uri("https://api.worldbank.org");
    }

    public async Task<IReadOnlyList<Metric>> FetchMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, string>
        {
            ["source"] = "worldbank",
            ["country"] = _countryCode,
        };

        var metrics = new List<Metric>();

        try
        {
            foreach (var (indicator, metricName) in Indicators)
            {
                var url = $"/v2/country/{_countryCode}/indicator/{indicator}?format=json&per_page=1&mrv=1";
                var json = await _http.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);

                // World Bank API returns an array: [metadata, data_array]
                var root = doc.RootElement;
                if (root.GetArrayLength() < 2)
                    continue;

                var dataArray = root[1];
                if (dataArray.GetArrayLength() == 0)
                    continue;

                var entry = dataArray[0];
                var valueElement = entry.GetProperty("value");

                if (valueElement.ValueKind == JsonValueKind.Number)
                {
                    metrics.Add(new Metric(metricName, valueElement.GetDouble(), now, tags));
                }
            }

            _logger.LogInformation("PublicDataSource fetched {Count} metrics", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PublicDataSource failed to fetch metrics");
            throw;
        }
    }
}
