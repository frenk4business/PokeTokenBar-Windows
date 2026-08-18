namespace PokeTokenBar.Core.Game;

public sealed record ProgressionResult(CompanionState State, IReadOnlyList<CompanionProgressEvent> Events);
