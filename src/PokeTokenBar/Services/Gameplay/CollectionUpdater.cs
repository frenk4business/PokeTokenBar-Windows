using PokeTokenBar.Core.Game;

namespace PokeTokenBar.Services.Gameplay;

public sealed class CollectionUpdater
{
    public GameSaveState ApplyEvents(GameSaveState state, IReadOnlyList<CompanionProgressEvent> events, DateTimeOffset now)
    {
        var next = state;
        foreach (var domainEvent in events)
        {
            next = domainEvent switch
            {
                Hatched hatched => ApplyHatched(next, hatched, now),
                Evolved evolved => ApplyEvolved(next, evolved, now),
                Graduated graduated => ApplyGraduated(next, graduated),
                _ => next
            };
        }

        return next;
    }

    public GameSaveState EnsureActiveLifecycle(GameSaveState state, DateTimeOffset now)
    {
        if (state.Companion.Active is null || state.ActiveCatch is not null)
        {
            return state;
        }

        var active = state.Companion.Active;
        var speciesId = active.CurrentSpeciesId;
        var displayName = NameFor(state, speciesId);
        var activeCatch = new CatchLogEntry
        {
            IndividualId = active.IndividualId,
            BaseSpeciesId = active.BaseSpeciesId,
            PlannedPathSpeciesIds = active.PlannedPathSpeciesIds,
            EncounteredSpeciesIds = active.RealizedSpeciesIds,
            FinalSpeciesId = active.CurrentSpeciesId,
            Rarity = active.Rarity,
            Nature = active.Nature,
            IsShiny = active.IsShiny,
            HatchTime = active.HatchTime,
            Status = CompanionLifecycleStatus.Active,
            TotalAppliedProgressTokens = active.TotalAppliedProgressTokens,
            EvolutionHistory = active.RealizedSpeciesIds.Select((id, index) =>
                new EvolutionHistoryEntry(id, NameFor(state, id), index == 0 ? active.HatchTime : now, index)).ToArray()
        };

        var pokedex = new Dictionary<int, PokedexSpeciesEntry>(state.Pokedex);
        foreach (var id in active.RealizedSpeciesIds)
        {
            UpsertPokedex(pokedex, id, NameFor(state, id), active.Rarity, active.IsShiny, active.HatchTime);
        }

        return state with { ActiveCatch = activeCatch, Pokedex = pokedex };
    }

    private GameSaveState ApplyHatched(GameSaveState state, Hatched hatched, DateTimeOffset now)
    {
        var active = state.Companion.Active;
        var individualId = active?.IndividualId ?? Guid.NewGuid().ToString("N");
        var plannedPath = active?.PlannedPathSpeciesIds ?? new[] { hatched.SpeciesId };
        var hatchTime = active?.HatchTime ?? now;
        var displayName = NameFor(state, hatched.SpeciesId);
        var activeCatch = new CatchLogEntry
        {
            IndividualId = individualId,
            BaseSpeciesId = active?.BaseSpeciesId ?? hatched.SpeciesId,
            PlannedPathSpeciesIds = plannedPath,
            EncounteredSpeciesIds = new[] { hatched.SpeciesId },
            FinalSpeciesId = hatched.SpeciesId,
            Rarity = hatched.Rarity,
            Nature = hatched.Nature,
            IsShiny = hatched.IsShiny,
            HatchTime = hatchTime,
            Status = CompanionLifecycleStatus.Active,
            TotalAppliedProgressTokens = active?.TotalAppliedProgressTokens ?? 0,
            EvolutionHistory = new[] { new EvolutionHistoryEntry(hatched.SpeciesId, displayName, hatchTime, 0) }
        };

        var pokedex = new Dictionary<int, PokedexSpeciesEntry>(state.Pokedex);
        UpsertPokedex(pokedex, hatched.SpeciesId, displayName, hatched.Rarity, hatched.IsShiny, hatchTime);
        return state with { ActiveCatch = activeCatch, Pokedex = pokedex };
    }

    private GameSaveState ApplyEvolved(GameSaveState state, Evolved evolved, DateTimeOffset now)
    {
        if (state.ActiveCatch is null)
        {
            return state;
        }

        var displayName = NameFor(state, evolved.ToSpeciesId);
        var encountered = state.ActiveCatch.EncounteredSpeciesIds.Contains(evolved.ToSpeciesId)
            ? state.ActiveCatch.EncounteredSpeciesIds
            : state.ActiveCatch.EncounteredSpeciesIds.Concat(new[] { evolved.ToSpeciesId }).ToArray();
        var history = state.ActiveCatch.EvolutionHistory.Any(entry => entry.SpeciesId == evolved.ToSpeciesId && entry.StageIndex == evolved.StageIndex)
            ? state.ActiveCatch.EvolutionHistory
            : state.ActiveCatch.EvolutionHistory.Concat(new[] { new EvolutionHistoryEntry(evolved.ToSpeciesId, displayName, now, evolved.StageIndex) }).ToArray();

        var activeCatch = state.ActiveCatch with
        {
            EncounteredSpeciesIds = encountered,
            FinalSpeciesId = evolved.ToSpeciesId,
            EvolutionHistory = history,
            TotalAppliedProgressTokens = state.Companion.Active?.TotalAppliedProgressTokens ?? state.ActiveCatch.TotalAppliedProgressTokens
        };

        var pokedex = new Dictionary<int, PokedexSpeciesEntry>(state.Pokedex);
        UpsertPokedex(pokedex, evolved.ToSpeciesId, displayName, state.ActiveCatch.Rarity, state.ActiveCatch.IsShiny, now);
        return state with { ActiveCatch = activeCatch, Pokedex = pokedex };
    }

    private GameSaveState ApplyGraduated(GameSaveState state, Graduated graduated)
    {
        var individualId = graduated.Companion.IndividualId ?? state.ActiveCatch?.IndividualId ?? Guid.NewGuid().ToString("N");
        if (state.CatchLog.Any(entry => entry.IndividualId == individualId && entry.Status == CompanionLifecycleStatus.Graduated))
        {
            return state with { ActiveCatch = null };
        }

        var active = state.ActiveCatch;
        var entry = (active ?? FromGraduation(state, graduated.Companion, individualId)) with
        {
            IndividualId = individualId,
            FinalSpeciesId = graduated.Companion.FinalSpeciesId,
            PlannedPathSpeciesIds = graduated.Companion.PlannedPathSpeciesIds,
            EncounteredSpeciesIds = graduated.Companion.RealizedSpeciesIds,
            GraduationTime = graduated.Companion.GraduationTime,
            Status = CompanionLifecycleStatus.Graduated,
            TotalAppliedProgressTokens = graduated.Companion.TotalAppliedProgressTokens
        };

        var catchLog = state.CatchLog.Where(existing => existing.IndividualId != individualId).Concat(new[] { entry }).ToArray();
        return state with { ActiveCatch = null, CatchLog = catchLog };
    }

    private static CatchLogEntry FromGraduation(GameSaveState state, GraduatedCompanion companion, string individualId)
    {
        return new CatchLogEntry
        {
            IndividualId = individualId,
            BaseSpeciesId = companion.BaseSpeciesId,
            PlannedPathSpeciesIds = companion.PlannedPathSpeciesIds,
            EncounteredSpeciesIds = companion.RealizedSpeciesIds,
            FinalSpeciesId = companion.FinalSpeciesId,
            Rarity = companion.Rarity,
            Nature = companion.Nature,
            IsShiny = companion.IsShiny,
            HatchTime = companion.HatchTime,
            GraduationTime = companion.GraduationTime,
            Status = CompanionLifecycleStatus.Graduated,
            TotalAppliedProgressTokens = companion.TotalAppliedProgressTokens,
            EvolutionHistory = companion.RealizedSpeciesIds.Select((id, index) =>
                new EvolutionHistoryEntry(id, NameFor(state, id), index == 0 ? companion.HatchTime : companion.GraduationTime, index)).ToArray()
        };
    }

    private static void UpsertPokedex(
        Dictionary<int, PokedexSpeciesEntry> pokedex,
        int speciesId,
        string displayName,
        Rarity rarity,
        bool shiny,
        DateTimeOffset encounteredAt)
    {
        if (pokedex.TryGetValue(speciesId, out var existing))
        {
            pokedex[speciesId] = existing with
            {
                DisplayName = string.IsNullOrWhiteSpace(existing.DisplayName) ? displayName : existing.DisplayName,
                Owned = true,
                ShinyOwned = existing.ShinyOwned || shiny,
                LatestEncounteredAt = encounteredAt
            };
            return;
        }

        pokedex[speciesId] = new PokedexSpeciesEntry
        {
            SpeciesId = speciesId,
            DisplayName = displayName,
            Owned = true,
            ShinyOwned = shiny,
            FirstEncounteredAt = encounteredAt,
            LatestEncounteredAt = encounteredAt,
            Generation = GenerationFor(speciesId),
            Rarity = rarity
        };
    }

    private static string NameFor(GameSaveState state, int speciesId)
    {
        return state.SpeciesNames.TryGetValue(speciesId, out var name) ? name : $"#{speciesId:000}";
    }

    private static int GenerationFor(int speciesId) => speciesId switch
    {
        >= 1 and <= 151 => 1,
        <= 251 => 2,
        <= 386 => 3,
        <= 493 => 4,
        <= 649 => 5,
        _ => 0
    };
}
