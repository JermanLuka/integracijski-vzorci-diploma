using System.Net;

namespace Shared.Tests;

/// <summary>
/// A test HTTP handler that returns canned responses based on request path + query.
/// Optionally simulates a transient outage: keys listed in <paramref name="transientFailures"/>
/// return 503 for their first N requests, then serve the canned response. Counting is
/// per key and thread-safe, because producers may run concurrently. Without transient
/// failures configured, behavior is unchanged.
/// </summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;
    private readonly Dictionary<string, int> _remainingFailures;
    private readonly object _lock = new();
    private int _requestCount;

    /// <summary>Total number of requests this handler has received.</summary>
    public int RequestCount => _requestCount;

    public FakeHttpHandler(Dictionary<string, string> responses, Dictionary<string, int>? transientFailures = null)
    {
        _responses = responses;
        _remainingFailures = transientFailures is not null ? new Dictionary<string, int>(transientFailures) : [];
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
