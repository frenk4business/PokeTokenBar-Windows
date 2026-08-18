using System.Net;
using System.Net.Http;

namespace PokeTokenBar.Tests.Providers.Sprites;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<HttpResponseMessage>> _responses = new(StringComparer.OrdinalIgnoreCase);

    public int RequestCount { get; private set; }

    public void Enqueue(string contains, HttpResponseMessage response)
    {
        if (!_responses.TryGetValue(contains, out var queue))
        {
            queue = new Queue<HttpResponseMessage>();
            _responses[contains] = queue;
        }

        queue.Enqueue(response);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        var url = request.RequestUri?.ToString() ?? string.Empty;
        foreach (var pair in _responses)
        {
            if (url.Contains(pair.Key, StringComparison.OrdinalIgnoreCase) && pair.Value.Count > 0)
            {
                return Task.FromResult(pair.Value.Dequeue());
            }
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public static HttpResponseMessage Bytes(params byte[] bytes)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }
}
