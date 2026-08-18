using PokeTokenBar.Core.Game;
using PokeTokenBar.Services.PokeApi;
using PokeTokenBar.Tests.Game;

namespace PokeTokenBar.Tests.Infrastructure;

internal sealed class FakeAsyncHatchService : IAsyncHatchService
{
    private readonly Queue<HatchResult> _results = new();

    public bool Fail { get; set; }

    public int Calls { get; private set; }

    public HatchRequest? LastRequest { get; private set; }

    public void Enqueue(HatchResult result)
    {
        _results.Enqueue(result);
    }

    public void Clear()
    {
        _results.Clear();
    }

    public Task<HatchResult> HatchAsync(HatchRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastRequest = request;
        if (Fail)
        {
            throw new PokeApiException("offline");
        }

        return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : GameFixtures.BulbasaurHatch());
    }
}
