using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Behavior;

/// <summary>Fase 16.3 T29 (COH-45): DecisionContextCache dirty-flag por categoria.</summary>
public class DecisionContextCacheTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly Personality Neutral =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static (WorldState World, Npc Npc) Build(ulong seed = 1)
    {
        var world = new WorldState(
            Calendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules);
        var loc = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30),
            new CultureId(1), loc, null, null, null, 100, Neutral, ProfessionType.None, loc,
            hunger: 40, thirst: 50, sleep: 60, social: 70);
        world.AddNpc(npc);
        return (world, npc);
    }

    [Fact]
    public void Cold_BuildIncremental_matches_full_Build()
    {
        var (world, npc) = Build(30);
        DecisionContextCache.ResetCounters();

        var full = DecisionContextBuilder.Build(world, npc, tick: 10);
        var incremental = DecisionContextCache.BuildIncremental(world, npc, tick: 10);

        Assert.Equal(full.Needs, incremental.Needs);
        Assert.Equal(full.Body, incremental.Body);
        Assert.Equal(full.RelevantMemories.Count, incremental.RelevantMemories.Count);
        Assert.Equal(full.KnownRelationships.Count, incremental.KnownRelationships.Count);
        Assert.Equal(full.PowerOpportunities.Count, incremental.PowerOpportunities.Count);
    }

    [Fact]
    public void Clean_Memory_category_does_not_rebuild_memories()
    {
        var (world, npc) = Build(31);
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "foi traído por X", importance: 90, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        _ = DecisionContextCache.BuildIncremental(world, npc, tick: 1);
        DecisionContextCache.ResetCounters();

        // Só Needs dirty — Memory/Belief/Relationship não devem rebuildar.
        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.Needs);
        npc.SetHunger(10, tick: 2);
        var ctx = DecisionContextCache.BuildIncremental(world, npc, tick: 2);

        Assert.Equal(1, DecisionContextCache.NeedsBuildCount);
        Assert.Equal(0, DecisionContextCache.MemoryBuildCount);
        Assert.Equal(0, DecisionContextCache.BeliefBuildCount);
        Assert.Equal(0, DecisionContextCache.RelationshipBuildCount);
        Assert.Equal(10, ctx.Needs.Hunger);
        Assert.NotEmpty(ctx.RelevantMemories);
    }

    [Fact]
    public void Dirty_Memory_rebuilds_and_matches_full_Build()
    {
        var (world, npc) = Build(32);
        _ = DecisionContextCache.BuildIncremental(world, npc, tick: 1);

        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "nova memória relevante fome", importance: 95, originTick: 2,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.Memory);
        DecisionContextCache.ResetCounters();
        var incremental = DecisionContextCache.BuildIncremental(world, npc, tick: 2);
        var full = DecisionContextBuilder.Build(world, npc, tick: 2);

        Assert.Equal(1, DecisionContextCache.MemoryBuildCount);
        Assert.Equal(full.RelevantMemories.Count, incremental.RelevantMemories.Count);
        Assert.Equal(
            full.RelevantMemories.Select(m => m.Content).OrderBy(c => c).ToArray(),
            incremental.RelevantMemories.Select(m => m.Content).OrderBy(c => c).ToArray());
    }

    [Fact]
    public void Dirty_Relationships_rebuild_matches_full_Build()
    {
        var (world, npc) = Build(33);
        _ = DecisionContextCache.BuildIncremental(world, npc, tick: 1);

        var other = new Npc(
            new NpcId(2), "other", Sex.Female, WorldDate.Epoch(Calendar).AddYears(-28),
            new CultureId(1), npc.CurrentLocation, null, null, null, 100, Neutral, ProfessionType.None,
            npc.CurrentLocation);
        world.AddNpc(other);
        var rel = world.GetOrCreateRelationship(new RelationshipKey(npc.Id, other.Id), now: 1);
        for (int i = 0; i < 10; i++)
            rel.ApplyEvent(RelationshipEventType.Cohabitation, ScenarioRunner.DefaultFamilyRules);

        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.Relationships);
        var incremental = DecisionContextCache.BuildIncremental(world, npc, tick: 2);
        var full = DecisionContextBuilder.Build(world, npc, tick: 2);

        Assert.Equal(full.KnownRelationships.Count, incremental.KnownRelationships.Count);
        Assert.NotEmpty(incremental.KnownRelationships);
    }

    [Fact]
    public void Full_reconstruct_and_incremental_All_dirty_are_identical()
    {
        var (world, npc) = Build(34);
        world.AddNpcMemory(
            npc.Id, MemoryCategory.Social, "traição", importance: 80, originTick: 1,
            participants: [npc.Id], location: npc.CurrentLocation,
            canonicalImportanceThreshold: LlmRules.Default.CanonicalMemoryImportanceThreshold);

        var full = DecisionContextBuilder.Build(world, npc, tick: 5);
        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.All);
        var incremental = DecisionContextCache.BuildIncremental(world, npc, tick: 5);

        Assert.Equal(full.Needs, incremental.Needs);
        Assert.Equal(full.Body.Height, incremental.Body.Height);
        Assert.Equal(full.RelevantMemories.Count, incremental.RelevantMemories.Count);
        Assert.Equal(full.RelevantBeliefs.Count, incremental.RelevantBeliefs.Count);
        Assert.Equal(full.KnownRelationships.Count, incremental.KnownRelationships.Count);
        Assert.Equal(full.PowerOpportunities.Count, incremental.PowerOpportunities.Count);
        Assert.Equal(full.CurrentAction, incremental.CurrentAction);
    }

    [Fact]
    public void MarkDirty_is_cumulative_across_categories()
    {
        var (world, npc) = Build(35);
        _ = DecisionContextCache.BuildIncremental(world, npc, tick: 1);
        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.Needs);
        DecisionContextCache.MarkDirty(world, npc.Id, DecisionContextCategory.Memory);
        DecisionContextCache.ResetCounters();

        _ = DecisionContextCache.BuildIncremental(world, npc, tick: 2);

        Assert.Equal(1, DecisionContextCache.NeedsBuildCount);
        Assert.Equal(1, DecisionContextCache.MemoryBuildCount);
        Assert.Equal(0, DecisionContextCache.RelationshipBuildCount);
    }
}
