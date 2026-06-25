using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Sources;

namespace Shared.Tests;

public class PublicDataSourceTests
{
    [Fact]
    public async Task FetchMetricsAsync_ReturnsThreeMetrics()
    {
        static string MakeResponse(double value)
        {
            var v = value.ToString("G", CultureInfo.InvariantCulture);
            return "[{\"page\":1,\"pages\":1,\"total\":1},[{\"value\":" + v + "}]]";
        }

        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["/v2/country/SVN/indicator/NY.GDP.MKTP.CD?format=json&per_page=1&mrv=1"] = MakeResponse(54000000000),
            ["/v2/country/SVN/indicator/SP.POP.TOTL?format=json&per_page=1&mrv=1"] = MakeResponse(2100000),
            ["/v2/country/SVN/indicator/SP.DYN.LE00.IN?format=json&per_page=1&mrv=1"] = MakeResponse(81.2),
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.worldbank.org") };
        var source = new PublicDataSource(http, "SVN", NullLogger<PublicDataSource>.Instance);

        var metrics = await source.FetchMetricsAsync();

        Assert.Equal(3, metrics.Count);
        Assert.Contains(metrics, m => m.Name == "worldbank.gdp_usd" && m.Value == 54000000000);
        Assert.Contains(metrics, m => m.Name == "worldbank.population" && m.Value == 2100000);
        Assert.Contains(metrics, m => m.Name == "worldbank.life_expectancy" && m.Value == 81.2);
    }
}
