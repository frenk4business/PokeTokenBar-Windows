using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.PokeApi;

public interface IAsyncHatchService
{
    Task<HatchResult> HatchAsync(HatchRequest request, CancellationToken cancellationToken = default);
}
