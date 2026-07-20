using ArchA.Hexagonal.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchA.Hexagonal.Adapters;

/// <summary>
/// Adapter that wraps GitHubSource behind the IMetricSource port.
/// </summary>
public sealed class GitHubAdapter : IMetricSource
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GitHubAdapter> _logger;
    private readonly HttpMessageHandler? _handler;

    public GitHubAdapter(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GitHubAdapter>();
        _handler = handler;
    }

    public string Name => "GitHub";

    public async Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
    {
        var token = _config["GitHub:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("GitHub token not configured, skipping");
            return [];
        }

        using var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
        var source = new GitHubSource(http, token, _loggerFactory.CreateLogger<GitHubSource>());
        return await source.FetchMetricsAsync(ct);
    }
}
