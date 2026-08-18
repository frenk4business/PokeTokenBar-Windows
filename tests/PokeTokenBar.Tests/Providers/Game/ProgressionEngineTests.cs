using PokeTokenBar.Core.Game;
using PokeTokenBar.Tests.Infrastructure;

namespace PokeTokenBar.Tests.Game;

public sealed class ProgressionEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EggBelowThresholdDoesNotHatch()
    {
        var hatch = new FixedHatchService(GameFixtures.BulbasaurHatch());
        var engine = new ProgressionEngine(hatch, new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), 4_999_999);

        Assert.True(result.State.IsEgg);
        Assert.Equal(4_999_999, result.State.Egg!.ProgressTokens);
        Assert.Null(result.State.Active);
        Assert.Equal(0, hatch.HatchCount);
    }

    [Fact]
    public void EggAtExactThresholdHatches()
    {
        var engine = new ProgressionEngine(new FixedHatchService(GameFixtures.BulbasaurHatch()), new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), GameBalance.EggHatchThreshold);

        Assert.False(result.State.IsEgg);
        Assert.Equal(1, result.State.Active!.CurrentSpeciesId);
        Assert.Contains(result.Events, item => item is Hatched);
    }

    [Fact]
    public void EggOverflowAppliesToHatchedPokemon()
    {
        var engine = new ProgressionEngine(new FixedHatchService(GameFixtures.BulbasaurHatch()), new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), GameBalance.EggHatchThreshold + 1);

        Assert.Equal(1, result.State.Active!.StageProgressTokens);
        Assert.Equal(1, result.State.Active.TotalAppliedProgressTokens);
    }

    [Fact]
    public void LargeEggOverflowCanEvolveNewPokemon()
    {
        var engine = new ProgressionEngine(new FixedHatchService(GameFixtures.BulbasaurHatch()), new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), GameBalance.EggHatchThreshold + 125_000_001);

        Assert.Equal(2, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(1, result.State.Active.StageIndex);
        Assert.Equal(1, result.State.Active.StageProgressTokens);
        Assert.Contains(result.Events, item => item is Evolved { FromSpeciesId: 1, ToSpeciesId: 2 });
    }

    [Fact]
    public void ActivePokemonJustBelowThresholdDoesNotEvolve()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: 124_999_998);

        var result = engine.ApplyProgress(state, 1);

        Assert.Equal(1, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(124_999_999, result.State.Active.StageProgressTokens);
    }

    [Fact]
    public void ActivePokemonAtExactThresholdEvolves()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: 124_999_999);

        var result = engine.ApplyProgress(state, 1);

        Assert.Equal(2, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(1, result.State.Active.StageIndex);
        Assert.Equal(0, result.State.Active.StageProgressTokens);
    }

    [Fact]
    public void OneTokenAboveEvolutionThresholdCarriesOverflow()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: 124_999_999);

        var result = engine.ApplyProgress(state, 2);

        Assert.Equal(2, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(1, result.State.Active.StageProgressTokens);
    }

    [Fact]
    public void LargeProgressionCanCrossMultipleEvolutions()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common);

        var result = engine.ApplyProgress(state, 125_000_000 + 250_000_000 + 5);

        Assert.Equal(3, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(2, result.State.Active.StageIndex);
        Assert.Equal(5, result.State.Active.StageProgressTokens);
        Assert.Equal(2, result.Events.OfType<Evolved>().Count());
    }

    [Fact]
    public void FinalStageExactThresholdGraduates()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageIndex: 2, stageProgress: 374_999_999);

        var result = engine.ApplyProgress(state, 1);

        Assert.True(result.State.IsEgg);
        Assert.Equal(0, result.State.Egg!.ProgressTokens);
        var graduated = Assert.Single(result.Events.OfType<Graduated>());
        Assert.Equal(3, graduated.Companion.FinalSpeciesId);
    }

    [Fact]
    public void FinalStageOverflowIsDiscardedAfterGraduation()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageIndex: 2, stageProgress: 374_999_999);

        var result = engine.ApplyProgress(state, 10_000_000);

        Assert.True(result.State.IsEgg);
        Assert.Equal(0, result.State.Egg!.ProgressTokens);
        Assert.Null(result.State.Active);
    }

    [Fact]
    public void GraduationSnapshotPreservesLifecycleDetails()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageIndex: 2, stageProgress: 374_999_999, nature: PokemonNature.Sassy, shiny: true);

        var result = engine.ApplyProgress(state, 1);

        var graduated = Assert.Single(result.Events.OfType<Graduated>()).Companion;
        Assert.Equal(1, graduated.BaseSpeciesId);
        Assert.Equal(3, graduated.FinalSpeciesId);
        Assert.Equal(new[] { 1, 2, 3 }, graduated.PlannedPathSpeciesIds);
        Assert.Equal(PokemonNature.Sassy, graduated.Nature);
        Assert.True(graduated.IsShiny);
        Assert.Equal(Now, graduated.GraduationTime);
    }

    [Fact]
    public void OneStagePokemonGraduatesWithoutFakeEvolution()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.LaprasPath(), Rarity.Rare, stageProgress: 2_999_999_999);

        var result = engine.ApplyProgress(state, 1);

        Assert.True(result.State.IsEgg);
        Assert.Empty(result.Events.OfType<Evolved>());
        Assert.Single(result.Events.OfType<Graduated>());
    }

    [Fact]
    public void TwoStagePokemonUsesOneTwoThresholdSplit()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.RattataPath(), Rarity.Common);

        var result = engine.ApplyProgress(state, 250_000_001);

        Assert.Equal(20, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(1, result.State.Active.StageIndex);
        Assert.Equal(1, result.State.Active.StageProgressTokens);
    }

    [Fact]
    public void BranchingPathPersistsThroughProgression()
    {
        var selectedPath = new EvolutionPath(new[] { GameFixtures.Eevee, GameFixtures.Jolteon });
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(selectedPath, Rarity.Uncommon);

        var result = engine.ApplyProgress(state, GameBalance.PhaseThreshold(Rarity.Uncommon, 2, 0));

        Assert.Equal(new[] { 133, 135 }, result.State.Active!.PlannedPathSpeciesIds);
        Assert.Equal(135, result.State.Active.CurrentSpeciesId);
    }

    [Fact]
    public void ShinyStateRemainsThroughEvolution()
    {
        var engine = new ProgressionEngine(new FixedHatchService(GameFixtures.BulbasaurHatch(shiny: true)), new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), GameBalance.EggHatchThreshold + 125_000_000);

        Assert.True(result.State.Active!.IsShiny);
        Assert.Equal(2, result.State.Active.CurrentSpeciesId);
    }

    [Fact]
    public void RareCandyProgressUsesNormalProgressionPath()
    {
        var engine = new ProgressionEngine(new FixedHatchService(), new FakeClock(Now));
        var state = GameFixtures.Active(GameFixtures.BulbasaurPath(), Rarity.Common, stageProgress: 50_000_000);

        var result = engine.ApplyProgress(state, GameBalance.RareCandyProgress);

        Assert.Equal(2, result.State.Active!.CurrentSpeciesId);
        Assert.Equal(25_000_000, result.State.Active.StageProgressTokens);
    }

    [Fact]
    public void HugeProgressionCrossesAllTransitionsUntilGraduation()
    {
        var engine = new ProgressionEngine(new FixedHatchService(GameFixtures.BulbasaurHatch()), new FakeClock(Now));

        var result = engine.ApplyProgress(CompanionState.FreshEgg(), GameBalance.EggHatchThreshold + 900_000_000);

        Assert.True(result.State.IsEgg);
        Assert.Equal(2, result.Events.OfType<Evolved>().Count());
        Assert.Single(result.Events.OfType<Graduated>());
        Assert.Equal(0, result.State.Egg!.ProgressTokens);
    }
}
