using PokeTokenBar.Core.Interfaces;

namespace PokeTokenBar.Core.Game;

public sealed class ProgressionEngine
{
    private readonly IHatchService _hatchService;
    private readonly IClock _clock;

    public ProgressionEngine(IHatchService hatchService, IClock clock)
    {
        _hatchService = hatchService;
        _clock = clock;
    }

    public ProgressionResult ApplyProgress(CompanionState state, long tokens)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (tokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokens), "Progression tokens cannot be negative.");
        }

        if (tokens == 0)
        {
            return new ProgressionResult(state, Array.Empty<CompanionProgressEvent>());
        }

        var events = new List<CompanionProgressEvent>();
        var current = state;
        var remaining = tokens;

        if (current.Egg is not null)
        {
            current = ApplyToEgg(current.Egg, remaining, events, out remaining);
        }

        if (remaining > 0 && current.Active is not null)
        {
            current = ApplyToActive(current.Active, remaining, events);
        }

        return new ProgressionResult(current, events);
    }

    private CompanionState ApplyToEgg(EggState egg, long tokens, List<CompanionProgressEvent> events, out long overflow)
    {
        var totalEggProgress = checked(egg.ProgressTokens + tokens);
        var appliedToEgg = Math.Min(tokens, GameBalance.EggHatchThreshold - egg.ProgressTokens);

        if (totalEggProgress < GameBalance.EggHatchThreshold)
        {
            overflow = 0;
            events.Add(new EggProgressed(tokens, totalEggProgress));
            return new CompanionState(egg with { ProgressTokens = totalEggProgress }, null);
        }

        overflow = totalEggProgress - GameBalance.EggHatchThreshold;
        events.Add(new EggProgressed(appliedToEgg, GameBalance.EggHatchThreshold));

        var hatchTime = _clock.Now;
        var hatch = _hatchService.Hatch(egg, hatchTime);
        var active = new ActiveCompanionState(
            hatch.BaseSpecies.NationalDexId,
            hatch.BaseSpecies.NationalDexId,
            hatch.SelectedPath.SpeciesIds,
            stageIndex: 0,
            stageProgressTokens: 0,
            hatch.Rarity,
            hatch.Nature,
            hatch.IsShiny,
            hatchTime);

        events.Add(new Hatched(appliedToEgg, active.CurrentSpeciesId, active.Rarity, active.Nature, active.IsShiny));
        return new CompanionState(null, active);
    }

    private CompanionState ApplyToActive(ActiveCompanionState active, long tokens, List<CompanionProgressEvent> events)
    {
        var current = active;
        var remaining = tokens;

        while (remaining > 0)
        {
            var threshold = GameBalance.PhaseThreshold(current.Rarity, current.PlannedPathSpeciesIds.Count, current.StageIndex);
            var needed = threshold - current.StageProgressTokens;
            var applied = Math.Min(remaining, needed);
            var newProgress = current.StageProgressTokens + applied;
            var newTotal = checked(current.TotalAppliedProgressTokens + applied);
            remaining -= applied;

            current = current with
            {
                StageProgressTokens = newProgress,
                TotalAppliedProgressTokens = newTotal
            };

            if (newProgress < threshold)
            {
                events.Add(new StageProgressed(applied, current.CurrentSpeciesId, current.StageIndex, current.StageProgressTokens));
                return new CompanionState(null, current);
            }

            var isFinalStage = current.StageIndex == current.PlannedPathSpeciesIds.Count - 1;
            if (isFinalStage)
            {
                var graduated = new GraduatedCompanion(
                    current.BaseSpeciesId,
                    current.CurrentSpeciesId,
                    current.PlannedPathSpeciesIds,
                    current.RealizedSpeciesIds,
                    current.Rarity,
                    current.Nature,
                    current.IsShiny,
                    current.HatchTime,
                    _clock.Now,
                    current.TotalAppliedProgressTokens,
                    current.IndividualId);

                events.Add(new Graduated(applied, graduated));

                // Upstream calls graduate(), resets to a new egg, and breaks the progression loop.
                // Any overflow after the final threshold is intentionally not carried into the fresh egg.
                return CompanionState.FreshEgg();
            }

            var fromSpeciesId = current.CurrentSpeciesId;
            var nextStageIndex = current.StageIndex + 1;
            var nextSpeciesId = current.PlannedPathSpeciesIds[nextStageIndex];
            var realized = current.RealizedSpeciesIds.Concat(new[] { nextSpeciesId }).ToArray();
            current = current with
            {
                CurrentSpeciesId = nextSpeciesId,
                StageIndex = nextStageIndex,
                StageProgressTokens = 0,
                RealizedSpeciesIds = realized
            };

            events.Add(new Evolved(applied, fromSpeciesId, nextSpeciesId, nextStageIndex));
        }

        return new CompanionState(null, current);
    }
}
