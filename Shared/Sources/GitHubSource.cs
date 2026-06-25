using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace Shared.Sources;

/// <summary>
/// Fetches developer metrics from the GitHub REST API v3.
/// Requires a personal access token.
/// </summary>
public sealed class GitHubSource
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly ILogger<GitHubSource> _logger;

    public GitHubSource(HttpClient http, string token, ILogger<GitHubSource> logger)
    {
        _http = http;
        _token = token;
        _logger = logger;

        _http.BaseAddress ??= new Uri("https://api.github.com");
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IntegracijskiVzorci", "1.0"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    public async Task<IReadOnlyList<Metric>> FetchMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tags = new Dictionary<string, string> { ["source"] = "github" };

        try
        {
            // Fetch user profile
            var userJson = await _http.GetStringAsync("/user", ct);
            using var userDoc = JsonDocument.Parse(userJson);
            var user = userDoc.RootElement;

            // Fetch repos to sum stars and forks
            var reposJson = await _http.GetStringAsync("/user/repos?per_page=100&type=owner", ct);
            using var reposDoc = JsonDocument.Parse(reposJson);

            int totalStars = 0, totalForks = 0;
            foreach (var repo in reposDoc.RootElement.EnumerateArray())
            {
                totalStars += repo.GetProperty("stargazers_count").GetInt32();
                totalForks += repo.GetProperty("forks_count").GetInt32();
            }

            var metrics = new List<Metric>
            {
                new("github.public_repos", user.GetProperty("public_repos").GetInt32(), now, tags),
                new("github.followers", user.GetProperty("followers").GetInt32(), now, tags),
                new("github.public_gists", user.GetProperty("public_gists").GetInt32(), now, tags),
                new("github.total_stars", totalStars, now, tags),
                new("github.total_forks", totalForks, now, tags),
            };

            _logger.LogInformation("GitHubSource fetched {Count} metrics", metrics.Count);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHubSource failed to fetch metrics");
            throw;
        }
    }
}
