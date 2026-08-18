namespace PokeTokenBar.Services.Sprites;

public sealed record SpriteResult(string Path, SpriteKind Kind, bool IsShiny, bool FromCache, bool IsPlaceholder = false);
