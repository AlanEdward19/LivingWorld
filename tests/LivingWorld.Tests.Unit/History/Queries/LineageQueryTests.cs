using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.History.Queries;

/// <summary>Fase 10, T18: <see cref="LineageQuery"/> (HIST-22/23).</summary>
public class LineageQueryTests
{
    private static readonly WorldCalendar Calendar = ScenarioRunner.DefaultCalendar;

    private static readonly Personality Personality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    [Fact]
    public void ReconstructFrom_reaches_founder_across_four_generations()
    {
        var (world, descendant) = BuildFourGenerationLineage();

        var result = LineageQuery.ReconstructFrom(descendant, world);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value!.Generations.Count);
        Assert.Null(result.Value.Generations[^1].MotherId);
        Assert.Null(result.Value.Generations[^1].FatherId);
    }

    [Fact]
    public void ReconstructFrom_fails_on_cycle()
    {
        var world = EmptyWorld();
        var a = AddNpc(world, new NpcId(1), mother: new NpcId(2));
        var b = AddNpc(world, new NpcId(2), mother: new NpcId(1));
        RecordBirthDeath(world, a.Id, birthTick: 10, deathTick: 100);
        RecordBirthDeath(world, b.Id, birthTick: 5, deathTick: 90);

        var result = LineageQuery.ReconstructFrom(a.Id, world);

        Assert.False(result.IsSuccess);
        Assert.Equal("cycle_detected", result.Error);
    }

    [Fact]
    public void ReconstructFrom_fails_on_hole()
    {
        var world = EmptyWorld();
        var child = AddNpc(world, new NpcId(1), mother: new NpcId(99));
        RecordBirthDeath(world, child.Id, birthTick: 10, deathTick: null);

        var result = LineageQuery.ReconstructFrom(child.Id, world);

        Assert.False(result.IsSuccess);
        Assert.Equal("hole_detected", result.Error);
    }

    [Fact]
    public void ReconstructFrom_fails_when_death_has_no_birth()
    {
        var world = EmptyWorld();
        var npc = AddNpc(world, new NpcId(1));
        world.AddFact(new Fact(new FactId(1), 100, WorldEventKind.Death, [npc.Id], null, 0.9, npc.Id.Value.ToString()));

        var result = LineageQuery.ReconstructFrom(npc.Id, world);

        Assert.False(result.IsSuccess);
        Assert.Equal("death_without_birth", result.Error);
    }

    [Fact]
    public void ReconstructFrom_fails_on_post_death_skeleton_event()
    {
        var world = EmptyWorld();
        var npc = AddNpc(world, new NpcId(1));
        RecordBirthDeath(world, npc.Id, birthTick: 10, deathTick: 100);
        world.AddFact(new Fact(new FactId(99), 150, WorldEventKind.Marriage, [npc.Id, new NpcId(2)], null, 0.8, "1|2"));

        var result = LineageQuery.ReconstructFrom(npc.Id, world);

        Assert.False(result.IsSuccess);
        Assert.Equal("post_death_event", result.Error);
    }

    private static (WorldState World, NpcId Descendant) BuildFourGenerationLineage()
    {
        var world = EmptyWorld();
        var founder = AddNpc(world, new NpcId(1));
        var gen2 = AddNpc(world, new NpcId(2), mother: founder.Id);
        var gen3 = AddNpc(world, new NpcId(3), mother: gen2.Id);
        var gen4 = AddNpc(world, new NpcId(4), mother: gen3.Id);
        var descendant = AddNpc(world, new NpcId(5), mother: gen4.Id);

        RecordBirthDeath(world, founder.Id, birthTick: 1, deathTick: 500);
        RecordBirthDeath(world, gen2.Id, birthTick: 50, deathTick: 600);
        RecordBirthDeath(world, gen3.Id, birthTick: 100, deathTick: 700);
        RecordBirthDeath(world, gen4.Id, birthTick: 150, deathTick: 800);
        RecordBirthDeath(world, descendant.Id, birthTick: 200, deathTick: null);

        return (world, descendant.Id);
    }

    private static WorldState EmptyWorld() => new(
        Calendar,
        seed: 1,
        ScenarioRunner.DefaultMap(1),
        ScenarioRunner.DefaultPopulationCatalog,
        ScenarioRunner.DefaultPopulationRules,
        ScenarioRunner.DefaultNeedsRules,
        ScenarioRunner.DefaultActionCatalog,
        ScenarioRunner.DefaultLifeStageRules,
        historyRules: HistoryRules.Default);

    private static Npc AddNpc(WorldState world, NpcId id, NpcId? mother = null, NpcId? father = null)
    {
        var npc = new Npc(
            id,
            $"npc-{id.Value}",
            Sex.Female,
            WorldDate.Epoch(Calendar),
            new CultureId(1),
            new CellCoord(0, 0),
            motherId: mother,
            fatherId: father,
            household: null,
            health: 100,
            personality: Personality,
            profession: new ProfessionType(1),
            currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return npc;
    }

    private static void RecordBirthDeath(WorldState world, NpcId npcId, long birthTick, long? deathTick)
    {
        world.AddFact(new Fact(
            world.NextFactIdAndAdvance(),
            birthTick,
            WorldEventKind.Birth,
            [npcId],
            null,
            0.9,
            npcId.Value.ToString()));
        if (deathTick is long death)
        {
            world.AddFact(new Fact(
                world.NextFactIdAndAdvance(),
                death,
                WorldEventKind.Death,
                [npcId],
                null,
                0.9,
                npcId.Value.ToString()));
        }
    }
}
