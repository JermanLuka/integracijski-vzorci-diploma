using Microsoft.Extensions.Logging.Abstractions;
using Shared.Sources;

namespace Shared.Tests;

public class StackOverflowSourceTests
{
    [Fact]
    public async Task FetchMetricsAsync_ReturnsFourMetrics()
    {
        var json = """
        {
            "items": [{
                "reputation": 1500,
                "badge_counts": {
                    "gold": 1,
                    "silver": 5,
                    "bronze": 20
                }
            }]
        }
        """;

        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["/2.3/me?site=stackoverflow&key=fake-key&access_token=fake-token&filter=default"] = json,
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.stackexchange.com") };
        var source = new StackOverflowSource(http, "fake-token", "fake-key", NullLogger<StackOverflowSource>.Instance);

        var metrics = await source.FetchMetricsAsync();

        Assert.Equal(4, metrics.Count);
        Assert.Contains(metrics, m => m.Name == "stackoverflow.reputation" && m.Value == 1500);
        Assert.Contains(metrics, m => m.Name == "stackoverflow.gold_badges" && m.Value == 1);
        Assert.Contains(metrics, m => m.Name == "stackoverflow.silver_badges" && m.Value == 5);
        Assert.Contains(metrics, m => m.Name == "stackoverflow.bronze_badges" && m.Value == 20);
    }
}
