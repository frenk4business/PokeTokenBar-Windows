using PokeTokenBar.Core.Game;
using PokeTokenBar.Providers.Codex;

namespace PokeTokenBar.Services.Gameplay;

public sealed record GameplayRefreshResult(
    GameSaveState State,
    CodexUsageSnapshot? UsageSnapshot,
    long AppliedDelta,
    IReadOnlyList<CompanionProgressEvent> Events,
    bool BaselineInitialized,
    bool HatchDeferred,
    string StatusMessage);
