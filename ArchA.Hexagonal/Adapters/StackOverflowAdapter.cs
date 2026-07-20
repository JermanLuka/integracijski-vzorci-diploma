using ArchA.Hexagonal.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchA.Hexagonal.Adapters;

/// <summary>
/// Adapter that wraps StackOverflowSource behind the IMetricSource port.
/// </summary>
public sealed class StackOverflowAdapter : IMetricSource
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<StackOverflowAdapter> _logger;
    private readonly HttpMessageHandler? _handler;

    public StackOverflowAdapter(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<StackOverflowAdapter>();
        _handler = handler;
    }

    public string Name => "StackOverflow";

    public async Task<IReadOnlyList<Metric>> FetchAsync(CancellationToken ct = default)
    {
        var accessToken = _config["StackOverflow:AccessToken"];
        var apiKey = _config["StackOverflow:ApiKey"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("StackOverflow credentials not configured, skipping");
            return [];
        }

        using var http = _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();
        var source = new StackOverflowSource(http, accessToken, apiKey, _loggerFactory.CreateLogger<StackOverflowSource>());
        return await source.FetchMetricsAsync(ct);
    }
}
