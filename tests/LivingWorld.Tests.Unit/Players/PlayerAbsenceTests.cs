using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-196 (Fase 25, #63): o sumiço do jogador é o sumiço de qualquer NPC, dói na medida do vínculo, e a vaga reaberta
/// é ocupada.</summary>
public class PlayerAbsenceTests
{
    internal sealed class AllEvents : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    internal sealed record Arm(WorldState World, WorldClock Clock, AllEvents Sink, TickContext Ctx);

    internal static Arm Village(ulong seed, PlayerRules? rules = null, int initialPopulation = ScenarioRunner.DefaultInitialPopulation,
        IReadOnlyList<ISimulationSystem>? systems = null)
    {
        var (world, _) = ScenarioRunner.Create(seed, initialPopulation: initialPopulation);
        world.UsePlayers(rules);
        var sink = new AllEvents();
        var clock = new WorldClock(systems ?? ScenarioRunner.DefaultSystems(), sink: sink);
        clock.Run(world, 96); // contratações acontecem nos primeiros dias
        sink.Events.Clear();
        return new Arm(world, clock, sink, new TickContext(world, world.Rng, world.Scheduler, sink));
    }

    internal static Npc Pick(WorldState world) =>
        world.Npcs.First(n => n.IsAlive && n.Employer is not null && n.Household is not null
            && world.FindHousehold(n.Household!.Value)!.Members.Count >= 3
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LifeStage.Adult);

    /// <summary>Hash canônico ano a ano por <paramref name="years"/> anos.</summary>
    private static List<string> Yearly(Arm arm, int years)
    {
        var hashes = new List<string> { WorldSnapshot.CanonicalHash(arm.World) };
        for (int y = 0; y < years; y++)
        {
            arm.Clock.Run(arm.World, arm.World.Calendar.HoursPerYear);
            hashes.Add(WorldSnapshot.CanonicalHash(arm.World));
        }
        return hashes;
    }

    private static (List<string> Engine, List<string> Player) OfflinePair(ulong seed, PlayerRules? rules, int years)
    {
        var engine = Village(seed, rules);
        Disappearance.Apply(engine.World, engine.Ctx, Pick(engine.World)); // o motor conduz X e X some em T

        var player = Village(seed, rules);
        var npc = Pick(player.World);
        Assert.True(PlayerCommands.Incarnate(player.World, player.Ctx, npc.Id, "alan").IsSuccess);
        Assert.True(PlayerCommands.Logout(player.World, player.Ctx, "alan").IsSuccess); // o jogador encarna X e sai em T

        return (Yearly(engine, years), Yearly(player, years));
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void A_player_going_offline_is_just_a_disappearance_byte_identical_for_ten_years()
    {
        var (engine, player) = OfflinePair(11, rules: null, years: 10);
        Assert.Equal(engine, player);
    }

    [Fact]
    public void With_AD196_off_the_incarnation_is_a_world_fact_and_the_two_arms_part()
    {
        var (engine, player) = OfflinePair(11, PlayerRules.Off, years: 0);
        Assert.NotEqual(engine[0], player[0]);
    }

    [Fact]
    public void With_AD196_off_the_disappearance_leaves_no_psyche_mark_and_the_return_no_reunion()
    {
        int Marks(PlayerRules? rules)
        {
            var arm = Village(4, rules);
            var npc = Pick(arm.World);
            Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
            Assert.True(PlayerCommands.Logout(arm.World, arm.Ctx, "alan").IsSuccess);
            Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess); // volta logo
            return arm.World.Npcs.Sum(n => n.Psyche?.Memories.Count(m => m.Other == npc.Id.Value
                && m.Kind is LifeEventKind.KinDisappeared or LifeEventKind.Reunited) ?? 0)
                + arm.Sink.Events.Count(e => e.Kind == WorldEventKind.Returned);
        }

        Assert.Equal(0, Marks(PlayerRules.Off));
        Assert.True(Marks(null) > 0);
    }

    /// <summary>Reação de quem ficou: dias de procura do que mais procurou, saliência da lembrança do sumiço na psique e a
    /// retirada do trabalho pelo luto (a rotina), somadas sobre a família.</summary>
    private static (int SearchDays, int Salience, double Withdrawal) Reaction(ulong seed, double bond)
    {
        var arm = Village(seed);
        var w = arm.World;
        var npc = Pick(w);
        var family = w.FindHousehold(npc.Household!.Value)!.Members.Where(m => m != npc.Id).Select(m => w.FindNpc(m)!).ToList();
        long now = w.CurrentDate.TotalHours;
        foreach (var relative in family)
            w.GetOrCreateRelationship(new RelationshipKey(relative.Id, npc.Id), now).CopyAxesFrom(Relationship.FromAxes(bond, bond, 50, 0, now));
        npc.CreditWallet(new Money(5000));

        Disappearance.Apply(w, arm.Ctx, npc);
        var elsewhere = new LivingWorld.Domain.Cities.City(
            w.NextCityId(), new LivingWorld.Domain.Geography.CellCoord(7, 7), 0, null, LivingWorld.Domain.Cities.AggregatePopulationPool.Empty);
        w.AddCity(elsewhere);
        npc.JoinCity(elsewhere.Id); // foi embora: ninguém o acha, cada um procura até onde o vínculo o leva

        arm.Clock.Run(w, 7 * w.Calendar.HoursPerDay);
        long week = w.CurrentDate.TotalHours;
        double withdrawal = family.Where(f => f.IsAlive).Sum(f => PsycheMechanic.WorkWithdrawal(w, f, week));
        int salience = family.Sum(f => f.Psyche?.Memories.Where(m => m.Kind == LifeEventKind.KinDisappeared && m.Other == npc.Id.Value)
            .Sum(m => m.Salience) ?? 0);
        arm.Clock.Run(w, 40 * w.Calendar.HoursPerDay);
        int days = w.CanonicalMemories.Concat(w.VolatileMemories)
            .Where(m => m.Participants.Contains(npc.Id) && m.Content.Contains("deixou de procurar", StringComparison.Ordinal))
            .Select(m => int.Parse(m.Content[(m.Content.LastIndexOf("depois de ", StringComparison.Ordinal) + 10)..].Split(' ')[0]))
            .DefaultIfEmpty(0).Max();
        return (days, salience, withdrawal);
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_disappearance_hurts_in_the_measure_of_the_bond_in_at_least_18_of_20_seeds()
    {
        int wins = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var weak = Reaction(seed, bond: 30);
            var strong = Reaction(seed, bond: 95);
            if (strong.SearchDays > weak.SearchDays && strong.Salience > weak.Salience && strong.Withdrawal > weak.Withdrawal) wins++;
        }

        Assert.True(wins >= 18, $"vínculo forte reagiu mais em {wins}/20 seeds");
    }

    [Fact]
    [Trait("Category", "Scenario")]
    public void The_reopened_job_is_filled_by_someone_else()
    {
        int filled = 0, tried = 0;
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var arm = Village(seed);
            var npc = Pick(arm.World);
            var workplace = arm.World.FindWorkplace(npc.Employer!.Value)!;
            Assert.True(PlayerCommands.Incarnate(arm.World, arm.Ctx, npc.Id, "alan").IsSuccess);
            Assert.True(PlayerCommands.Logout(arm.World, arm.Ctx, "alan").IsSuccess);
            tried++;
            arm.Clock.Run(arm.World, arm.World.Calendar.HoursPerYear);
            if (arm.Sink.Events.Any(e => e.Kind == WorldEventKind.Hired && e.Payload.EndsWith($"|{workplace.Id.Value}", StringComparison.Ordinal)
                    && !e.Payload.StartsWith($"{npc.Id.Value}|", StringComparison.Ordinal)))
                filled++;
        }

        Assert.True(filled * 10 >= tried * 8, $"vaga reaberta ocupada em {filled}/{tried} seeds");
    }
}
