using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Models;
using Shared.Sources;

namespace ArchB.Monolith;

/// <summary>
/// Data access layer - directly creates and calls the three metric sources.
/// </summary>
public sealed class DataAccess
{
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DataAccess> _logger;
    private readonly HttpMessageHandler? _handler;
    private readonly bool _failFast;

    public DataAccess(IConfiguration config, ILoggerFactory loggerFactory, HttpMessageHandler? handler = null, bool failFast = false)
    {
        _config = config;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DataAccess>();
        _handler = handler;
        _failFast = failFast;
    }

    private HttpClient CreateHttpClient() =>
        _handler is not null ? new HttpClient(_handler, disposeHandler: false) : new HttpClient();

    public async Task<IReadOnlyList<Metric>> FetchGitHubAsync(CancellationToken ct = default)
    {
        var token = _config["GitHub:Token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("GitHub token not configured, skipping GitHub source");
            return [];
        }

        try
        {
            var source = new GitHubSource(CreateHttpClient(), token, _loggerFactory.CreateLogger<GitHubSource>());
            return await source.FetchMetricsAsync(ct);
        }
        catch when (!_failFast)
        {
            _logger.LogWarning("GitHub source failed, skipping");
            return [];
        }
    }

    public async Task<IReadOnlyList<Metric>> FetchStackOverflowAsync(CancellationToken ct = default)
    {
        var accessToken = _config["StackOverflow:AccessToken"];
        var apiKey = _config["StackOverflow:ApiKey"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("StackOverflow credentials not configured, skipping StackOverflow source");
            return [];
        }

        try
        {
            var source = new StackOverflowSource(CreateHttpClient(), accessToken, apiKey, _loggerFactory.CreateLogger<StackOverflowSource>());
            return await source.FetchMetricsAsync(ct);
        }
        catch when (!_failFast)
        {
            _logger.LogWarning("StackOverflow source failed, skipping");
            return [];
        }
    }

    public async Task<IReadOnlyList<Metric>> FetchWorldBankAsync(CancellationToken ct = default)
    {
        var countryCode = _config["WorldBank:CountryCode"] ?? "SVN";

        try
        {
            var source = new PublicDataSource(CreateHttpClient(), countryCode, _loggerFactory.CreateLogger<PublicDataSource>());
            return await source.FetchMetricsAsync(ct);
        }
        catch when (!_failFast)
        {
            _logger.LogWarning("WorldBank source failed, skipping");
            return [];
        }
    }

    public async Task<IReadOnlyList<Metric>> FetchOpenMeteoAsync(CancellationToken ct = default)
    {
        var latitude = _config["OpenMeteo:Latitude"] ?? "46.05";
        var longitude = _config["OpenMeteo:Longitude"] ?? "14.51";

        try
        {
            var source = new OpenMeteoSource(CreateHttpClient(), latitude, longitude, _loggerFactory.CreateLogger<OpenMeteoSource>());
            return await source.FetchMetricsAsync(ct);
        }
        catch when (!_failFast)
        {
            _logger.LogWarning("OpenMeteo source failed, skipping");
            return [];
        }
    }
}
