using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-168 (pedido do dono): a carga de quem morre fica onde ele morreu. Em casa, fica com a casa; longe, fica no chão
/// para a casa buscar (numa viagem de verdade, levando o que couber) ou para quem passa furtar; o perecível estraga e a conta do
/// mundo fecha com o que está no chão.</summary>
public class LeavingsTests
{
    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    private static (WorldState World, TickContext Ctx, Sink Sink) NewWorld(InventionRules? rules = null, TheftRules? theft = null)
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: rules ?? ScenarioRunner.DefaultInventionRules, theftRules: theft);
        var sink = new Sink();
        return (world, new TickContext(world, world.Rng, world.Scheduler, sink), sink);
    }

    private static Household HomeOf(WorldState world, Npc npc) => world.FindHousehold(npc.Household!.Value)!;

    /// <summary>Um adulto de casa com mais gente e outro adulto da mesma casa.</summary>
    private static (Npc Dead, Npc Kin) Pair(WorldState world)
    {
        foreach (var npc in world.Npcs.Where(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20).OrderBy(n => n.Id.Value))
        {
            var kin = HomeOf(world, npc).Members.Select(m => world.FindNpc(m)!)
                .FirstOrDefault(m => m.Id != npc.Id && m.IsAlive && m.AgeYears(world.CurrentDate) >= 20);
            if (kin is not null) return (npc, kin);
        }
        throw new InvalidOperationException("sem casa com dois adultos");
    }

    private static CellCoord FarFrom(WorldState world, Npc npc, Household home) =>
        world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X)
            .First(c => LivingWorld.Simulation.Emergence.Mobility.TravelTicks(world, npc, c, home.Location, 1.0) is > 3 and < 12);

    private static ResourceType Food(WorldState world) => new(world.EconomyRules.FoodResourceId);

    /// <summary>O morto leva 3 de comida e morre em <paramref name="where"/>.</summary>
    private static void DieCarrying(WorldState world, TickContext ctx, Npc dead, CellCoord where)
    {
        world.Invention!.Carry(dead.Id.Value, Food(world).Id, 3);
        dead.MoveTo(where, world.CurrentDate.TotalHours);
        NpcDeath.Apply(world, ctx, dead, WorldEventKind.Accident);
    }

    [Fact]
    public void Leavings_death_at_home_keeps_the_load_with_the_household()
    {
        var (world, ctx, _) = NewWorld();
        var (dead, _) = Pair(world);
        var home = HomeOf(world, dead);
        long before = home.Stock.GetValueOrDefault(Food(world));

        DieCarrying(world, ctx, dead, home.Location);

        Assert.True(home.Stock.GetValueOrDefault(Food(world)) == before + 3 && world.Invention!.Leavings is null);
    }

    [Fact]
    public void Leavings_death_far_from_home_leaves_the_load_on_the_ground_where_he_fell()
    {
        var (world, ctx, sink) = NewWorld();
        var (dead, kin) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        kin.MoveTo(home.Location, world.CurrentDate.TotalHours);
        long before = home.Stock.GetValueOrDefault(Food(world));

        DieCarrying(world, ctx, dead, far);

        var leaving = Assert.Single(world.Invention!.Leavings!);
        Assert.True(leaving.X == far.X && leaving.Y == far.Y && leaving.HouseholdId == home.Id.Value
            && world.Invention.LeftQuantity(leaving.Id, Food(world).Id) == 3 && home.Stock.GetValueOrDefault(Food(world)) == before
            && sink.Events.Any(e => e.Kind == WorldEventKind.LeavingsLeft), "nada se teletransporta para a despensa");
    }

    [Fact]
    public void Leavings_off_returns_the_load_to_the_pantry_at_once_as_before()
    {
        var (world, ctx, _) = NewWorld(ScenarioRunner.DefaultInventionRules with { LeavingsOverride = LeavingsRules.Disabled });
        var (dead, _) = Pair(world);
        var home = HomeOf(world, dead);
        long before = home.Stock.GetValueOrDefault(Food(world));

        DieCarrying(world, ctx, dead, FarFrom(world, dead, home));

        Assert.True(home.Stock.GetValueOrDefault(Food(world)) == before + 3 && world.Invention!.Leavings is null);
    }

    [Fact]
    public void Leavings_kin_with_free_time_goes_to_fetch_and_brings_home_what_fits()
    {
        var (world, ctx, sink) = NewWorld();
        var (dead, kin) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        kin.MoveTo(home.Location, world.CurrentDate.TotalHours);
        kin.SetCurrentAction(ActionType.Idle, world.CurrentDate.TotalHours);
        DieCarrying(world, ctx, dead, far);
        long pantry = home.Stock.GetValueOrDefault(Food(world));

        long fits = Math.Min(3, Fits(world, kin));

        bool goes = Leavings.WantsToFetch(world, kin, world.CurrentDate.TotalHours);
        kin.SetCurrentAction(ActionType.Travel, world.CurrentDate.TotalHours); // a decisão vira a viagem
        var errand = Leavings.ErrandDestination(world, kin);
        kin.MoveTo(far, world.CurrentDate.TotalHours);
        Leavings.OnArrived(world, ctx, kin, far);
        bool carries = world.Invention!.CarriedQuantity(kin.Id.Value, Food(world).Id) == fits;
        PersonalCarry.Unpack(world, kin, home);

        Assert.True(goes && errand == far && carries && world.Invention.GroundTotal(Food(world).Id) == 3 - fits
            && home.Stock.GetValueOrDefault(Food(world)) == pantry + fits
            && sink.Events.Any(e => e.Kind == WorldEventKind.LeavingsCollected && e.Payload!.EndsWith("|casa", StringComparison.Ordinal))
            && !sink.Events.Any(e => e.Kind == WorldEventKind.TheftCommitted), "pegar o que é da própria casa não é furto");
    }

    /// <summary>A busca de verdade, pela decisão do motor: em três dias alguém da casa vai ao lugar e recolhe (a viagem custa o
    /// caminho; ninguém recolhe na hora porque ninguém da casa estava lá).</summary>
    [Fact]
    public void Leavings_the_household_really_travels_to_fetch_within_days()
    {
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: ScenarioRunner.DefaultInventionRules);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var (dead, _) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        DieCarrying(world, ctx, dead, far);
        bool leftThere = world.Invention!.Leavings is { Count: 1 };

        clock.Run(world, 3 * world.Calendar.HoursPerDay);

        Assert.True(leftThere && world.InventionTally.LeavingsFetches >= 1 && world.InventionTally.LeavingsCollected >= 1,
            $"idas {world.InventionTally.LeavingsFetches}, recolhidos {world.InventionTally.LeavingsCollected}, furtados {world.InventionTally.LeavingsStolen}");
    }

    /// <summary>Unidades de comida que cabem no que a pessoa leva (peso e volume), sem nada levado.</summary>
    private static long Fits(WorldState world, Npc npc)
    {
        var room = PersonalCarry.RoomOf(world, npc);
        int food = world.EconomyRules.FoodResourceId;
        return Math.Min(room.WeightG / world.InventionRules!.UnitGramsOf(food), room.DryMl / world.InventionRules.UnitMlOf(food));
    }

    [Fact]
    public void Leavings_fetch_takes_only_what_the_body_can_carry()
    {
        var (world, ctx, _) = NewWorld();
        var (dead, kin) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        var food = Food(world);
        int unitG = world.InventionRules!.UnitGramsOf(food.Id);
        int many = world.InventionRules.FootCarryKg * 1000 / unitG + 10;
        world.Invention!.Carry(dead.Id.Value, food.Id, many - 3);
        DieCarrying(world, ctx, dead, far);

        kin.MoveTo(far, world.CurrentDate.TotalHours);
        Leavings.OnArrived(world, ctx, kin, far);

        var room = PersonalCarry.RoomOf(world, kin);
        long fits = Math.Min(room.WeightG / unitG, room.DryMl / world.InventionRules.UnitMlOf(food.Id));
        long left = world.Invention.Leavings is { Count: 1 } l ? world.Invention.LeftQuantity(l[0].Id, food.Id) : 0;
        Assert.True(world.Invention.CarriedQuantity(kin.Id.Value, food.Id) == fits && left == many - fits, $"levou {world.Invention.CarriedQuantity(kin.Id.Value, food.Id)}, cabia {fits}, ficou {left}");
    }

    [Fact]
    public void Leavings_a_stranger_passing_by_can_steal_through_the_usual_theft()
    {
        var theft = TheftRules.Default with { MoralFloor = -1000, PersonalityThreshold = -1000, BaseSuccess = 1.0 };
        var (world, ctx, sink) = NewWorld(ScenarioRunner.DefaultInventionRules with { LeavingsOverride = LeavingsRules.Default with { TheftChancePm = 1000 } }, theft);
        var (dead, _) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        DieCarrying(world, ctx, dead, far);
        var stranger = world.Npcs.Where(n => n.IsAlive && n.Household is { } h && h != home.Id && n.AgeYears(world.CurrentDate) >= 20)
            .OrderBy(n => n.Id.Value).First();
        foreach (var other in world.AliveNpcIndex.At(far).ToList()) other.MoveTo(home.Location, world.CurrentDate.TotalHours);

        stranger.MoveTo(far, world.CurrentDate.TotalHours);
        Leavings.OnArrived(world, ctx, stranger, far);

        Assert.True(sink.Events.Any(e => e.Kind == WorldEventKind.TheftCommitted && e.Payload!.Contains("|espolio|", StringComparison.Ordinal))
            && world.Invention!.CarriedQuantity(stranger.Id.Value, Food(world).Id) == Math.Min(3, Fits(world, stranger)));
    }

    [Fact]
    public void Leavings_nobody_steals_while_someone_of_the_household_stands_there()
    {
        var theft = TheftRules.Default with { MoralFloor = -1000, PersonalityThreshold = -1000, BaseSuccess = 1.0 };
        var (world, ctx, sink) = NewWorld(ScenarioRunner.DefaultInventionRules with { LeavingsOverride = LeavingsRules.Default with { TheftChancePm = 1000 } }, theft);
        var (dead, kin) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        DieCarrying(world, ctx, dead, far);
        var stranger = world.Npcs.Where(n => n.IsAlive && n.Household is { } h && h != home.Id && n.AgeYears(world.CurrentDate) >= 20)
            .OrderBy(n => n.Id.Value).First();
        kin.MoveTo(far, world.CurrentDate.TotalHours);
        kin.SetCurrentAction(ActionType.Idle, world.CurrentDate.TotalHours);

        stranger.MoveTo(far, world.CurrentDate.TotalHours);
        Leavings.OnArrived(world, ctx, stranger, far);

        Assert.DoesNotContain(sink.Events, e => e.Kind == WorldEventKind.TheftCommitted);
    }

    [Fact]
    public void Leavings_ground_counts_in_the_world_stock_and_spoils_by_the_pantry_rate()
    {
        var (world, ctx, sink) = NewWorld();
        var (dead, _) = Pair(world);
        var food = Food(world);
        world.Invention!.Carry(dead.Id.Value, food.Id, 200);
        DieCarrying(world, ctx, dead, FarFrom(world, dead, HomeOf(world, dead)));
        long ground = world.Invention.GroundTotal(food.Id);

        Leavings.Spoil(world, ctx, world.EconomyRules, 30);

        long lost = sink.Events.Where(e => e.Kind == WorldEventKind.ResourceLost && e.Payload!.StartsWith("espolio-", StringComparison.Ordinal))
            .Sum(e => long.Parse(e.Payload!.Split('|')[2], System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(ground == 203 && lost > 0 && world.Invention.GroundTotal(food.Id) + lost == ground, $"chão {ground}, estragou {lost}");
    }

    [Fact]
    public void Leavings_heir_household_fetches_when_the_dead_lived_alone()
    {
        var (world, ctx, _) = NewWorld();
        var (dead, kin) = Pair(world);
        var home = HomeOf(world, dead);
        var far = FarFrom(world, dead, home);
        DieCarrying(world, ctx, dead, far);
        var leaving = world.Invention!.Leavings![0];
        var forged = leaving with { HouseholdId = -1, HeirHouseholdId = home.Id.Value }; // a casa do morto se desfez: o herdeiro é a de kin

        Assert.True(Leavings.ClaimKind(world, forged, kin) == "herdeiro" && Leavings.ClaimantHousehold(world, forged) == home.Id.Value);
    }

    [Fact]
    public void Leavings_snapshot_round_trip_keeps_the_ground_and_the_hash()
    {
        var (world, ctx, _) = NewWorld();
        var (dead, _) = Pair(world);
        DieCarrying(world, ctx, dead, FarFrom(world, dead, HomeOf(world, dead)));

        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        Assert.True(reopened.Invention!.Leavings!.SequenceEqual(world.Invention!.Leavings!)
            && reopened.Invention.LeftLoads!.SequenceEqual(world.Invention.LeftLoads!)
            && reopened.Invention.LastLeavingId == world.Invention.LastLeavingId
            && WorldSnapshot.CanonicalHash(world) == WorldSnapshot.CanonicalHash(reopened));
    }

    [Fact]
    public void Leavings_the_ground_is_in_the_world_hash()
    {
        var (world, ctx, _) = NewWorld();
        var (dead, _) = Pair(world);
        DieCarrying(world, ctx, dead, FarFrom(world, dead, HomeOf(world, dead)));
        string before = WorldSnapshot.CanonicalHash(world);
        var leaving = world.Invention!.Leavings![0];

        world.Invention.Leave(leaving.Id, Food(world).Id, -1);

        Assert.NotEqual(before, WorldSnapshot.CanonicalHash(world));
    }
}
