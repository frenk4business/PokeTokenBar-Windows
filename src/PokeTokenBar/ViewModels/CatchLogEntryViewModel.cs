using System.Windows.Media;

namespace PokeTokenBar.ViewModels;

public sealed record CatchLogEntryViewModel(
    string IndividualId,
    string Title,
    string PathSummary,
    string Detail,
    ImageSource? Sprite);
