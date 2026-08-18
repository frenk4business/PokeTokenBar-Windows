using System.Windows.Media;

namespace PokeTokenBar.ViewModels;

public sealed record CollectionSpeciesViewModel(
    int SpeciesId,
    string DisplayNumber,
    string DisplayName,
    string Rarity,
    string Generation,
    bool Owned,
    bool ShinyOwned,
    ImageSource? Sprite);
