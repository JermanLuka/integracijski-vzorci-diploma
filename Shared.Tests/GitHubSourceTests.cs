using Microsoft.Extensions.Logging.Abstractions;
using Shared.Sources;

namespace Shared.Tests;

public class GitHubSourceTests
{
    [Fact]
    public async Task FetchMetricsAsync_ReturnsFiveMetrics()
    {
        var userJson = """
        {
            "public_repos": 10,
            "followers": 25,
            "public_gists": 3
        }
        """;

        var reposJson = """
        [
            { "stargazers_count": 5, "forks_count": 2 },
            { "stargazers_count": 12, "forks_count": 4 }
        ]
        """;

        var handler = new FakeHttpHandler(new Dictionary<string, string>
        {
            ["/user"] = userJson,
            ["/user/repos?per_page=100&type=owner"] = reposJson,
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com") };
        var source = new GitHubSource(http, "fake-token", NullLogger<GitHubSource>.Instance);

        var metrics = await source.FetchMetricsAsync();

        Assert.Equal(5, metrics.Count);
        Assert.Contains(metrics, m => m.Name == "github.public_repos" && m.Value == 10);
        Assert.Contains(metrics, m => m.Name == "github.followers" && m.Value == 25);
        Assert.Contains(metrics, m => m.Name == "github.public_gists" && m.Value == 3);
        Assert.Contains(metrics, m => m.Name == "github.total_stars" && m.Value == 17);
        Assert.Contains(metrics, m => m.Name == "github.total_forks" && m.Value == 6);
    }
}
