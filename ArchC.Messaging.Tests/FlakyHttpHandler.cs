using System.Net;

namespace ArchC.Messaging.Tests;

/// <summary>
/// A test HTTP handler that simulates a transient source outage: configured paths
/// fail with 503 for their first N requests, then return the canned response.
/// Paths without a configured failure count behave like <see cref="Shared.Tests.FakeHttpHandler"/>.
/// </summary>
public sealed class FlakyHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;
    private readonly Dictionary<string, int> _remainingFailures;
    private readonly object _lock = new();
    private int _requestCount;

    public int RequestCount => _requestCount;

    public FlakyHttpHandler(Dictionary<string, string> responses, Dictionary<string, int> failuresPerPath)
    {
        _responses = responses;
        _remainingFailures = new Dictionary<string, int>(failuresPerPath);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = request.RequestUri!.PathAndQuery;

        lock (_lock)
        {
            _requestCount++;

            if (_remainingFailures.TryGetValue(key, out var remaining) && remaining > 0)
            {
                _remainingFailures[key] = remaining - 1;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent($"Transient failure for: {key}"),
                });
            }
        }

        if (!_responses.TryGetValue(key, out var body))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No canned response for: {key}"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        });
    }
}
