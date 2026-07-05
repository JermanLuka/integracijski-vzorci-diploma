using Microsoft.Extensions.Logging.Abstractions;
using Shared.Sources;

namespace Shared.Tests;

public class OpenMeteoSourceTests
{
    [Fact]
    public async Task FetchMetricsAsync_ReturnsThreeMetrics()
    {
        var response = """
        {
            "current": {
                "time": "2025-01-01T12:00",
                "interval": 900,
                "temperature_2m": 5.3,
                "relative_humidity_2m": 82,
                "wind_speed_10m": 12.7
            }
        }
        """;

        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["/v1/forecast?latitude=46.05&longitude=14.51&current=temperature_2m,relative_humidity_2m,wind_speed_10m"] = response,
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com") };
        var source = new OpenMeteoSource(http, "46.05", "14.51", NullLogger<OpenMeteoSource>.Instance);

        var metrics = await source.FetchMetricsAsync();

        Assert.Equal(3, metrics.Count);
        Assert.Contains(metrics, m => m.Name == "weather.temperature" && m.Value == 5.3);
        Assert.Contains(metrics, m => m.Name == "weather.humidity" && m.Value == 82);
        Assert.Contains(metrics, m => m.Name == "weather.wind_speed" && m.Value == 12.7);
    }
}
