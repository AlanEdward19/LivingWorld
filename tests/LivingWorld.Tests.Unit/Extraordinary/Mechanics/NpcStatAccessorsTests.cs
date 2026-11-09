using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Mechanics;

/// <summary>Issue #64: a tabela única lê/escreve cada atributo certo, e cada mecânica
/// preserva sua regra de clamp e o NPC (portador x alvo) que lê.</summary>
public sealed class NpcStatAccessorsTests
{
    private const long Tick = 0;

    [Fact]
    public void Each_stat_reads_its_own_npc_value_and_writes_only_that_value()
    {
        foreach (var stat in NpcStatAccessors.All)
        {
            var npc = DistinctNpc();
            Assert.Equal(Expected(stat.Key), stat.Read(npc, Tick));

            stat.Write(npc, 7, Tick);

            foreach (var other in NpcStatAccessors.All)
                Assert.Equal(other.Key == stat.Key ? 7 : Expected(other.Key), other.Read(npc, Tick));
        }
    }

    [Fact]
    public void Cost_availability_reads_the_carrier_not_the_target_for_every_stat()
    {
        var (ctx, carrier, target) = Context();
        Distinct(carrier, offset: 0);
        Distinct(target, offset: 5);

        foreach (var stat in NpcStatAccessors.All)
        {
            var available = new CarrierCostMechanic().CostAvailable(ctx, $"carrier.{stat.Key}");

            Assert.True(available.IsSuccess, available.Error);
            Assert.Equal(stat.Read(carrier, Tick), available.Value);
            Assert.NotEqual(stat.Read(target, Tick), available.Value);
        }
    }

    [Fact]
    public void Npc_effect_changes_only_the_named_stat_and_clamps_to_the_range()
    {
        foreach (var stat in NpcStatAccessors.All)
        {
            var (ctx, _, target) = Context();
            Distinct(target, offset: 0);
            var effect = new NpcStatMechanic().PrepareEffect(ctx, $"npc.{stat.Key}:-5");
            effect.Value!.Apply(ResolutionResult.Success);

            foreach (var other in NpcStatAccessors.All)
                Assert.Equal(other.Key == stat.Key ? Expected(other.Key) - 5 : Expected(other.Key), other.Read(target, Tick));

            var over = new NpcStatMechanic().PrepareEffect(ctx, $"npc.{stat.Key}:500");
            over.Value!.Apply(ResolutionResult.Success);
            Assert.Equal(stat.Max, stat.Read(target, Tick));
        }
    }

    [Fact]
    public void Carrier_cost_subtracts_from_the_carrier_only_and_floors_at_the_minimum()
    {
        foreach (var stat in NpcStatAccessors.All)
        {
            var (ctx, carrier, target) = Context();
            Distinct(carrier, offset: 0);
            Distinct(target, offset: 0);
            var cost = new CarrierCostMechanic().PrepareCost(ctx, $"carrier.{stat.Key}:3", 3);
            cost.Value!.Apply(ResolutionResult.Success);

            Assert.Equal(Expected(stat.Key) - 3, stat.Read(carrier, Tick));
            Assert.Equal(Expected(stat.Key), stat.Read(target, Tick));

            var drain = new CarrierCostMechanic().PrepareCost(ctx, $"carrier.{stat.Key}:900", 900);
            drain.Value!.Apply(ResolutionResult.Success);
            Assert.Equal(stat.Min, stat.Read(carrier, Tick));
        }
    }

    [Fact]
    public void Transfer_moves_the_named_stat_from_carrier_to_target_with_recipient_clamp()
    {
        foreach (var stat in NpcStatAccessors.All)
        {
            var (ctx, carrier, target) = Context();
            Distinct(carrier, offset: 0);
            Distinct(target, offset: 0);
            stat.Write(target, 95, Tick);
            var transfer = new TransferMechanic().PrepareEffect(ctx, $"transfer.{stat.Key}:10");
            transfer.Value!.Apply(ResolutionResult.Success);

            Assert.Equal(Expected(stat.Key) - 10, stat.Read(carrier, Tick));
            Assert.Equal(100, stat.Read(target, Tick));
        }
    }

    [Fact]
    public void Add_clamped_saturates_at_the_range_for_extreme_magnitudes()
    {
        foreach (var stat in NpcStatAccessors.All)
        {
            var npc = DistinctNpc();
            stat.AddClamped(npc, int.MaxValue, Tick);
            Assert.Equal(stat.Max, stat.Read(npc, Tick));

            stat.AddClamped(npc, int.MinValue, Tick);
            Assert.Equal(stat.Min, stat.Read(npc, Tick));
        }
    }

    private static int Expected(string key) => 10 + NpcStatAccessors.All.Select(stat => stat.Key).ToList().IndexOf(key) * 10;

    private static void Distinct(Npc npc, int offset)
    {
        foreach (var stat in NpcStatAccessors.All)
            stat.Write(npc, Expected(stat.Key) + offset, Tick);
    }

    private static Npc DistinctNpc()
    {
        var npc = NewNpc(new NpcId(9));
        Distinct(npc, 0);
        return npc;
    }

    private static (ExtraordinaryMechanicContext Ctx, Npc Carrier, Npc Target) Context()
    {
        var descriptor = new PowerDescriptor(
            "p", "s", ["npc.health:1"], "Active", [], "Guaranteed", [], [], [], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]));
        var carrier = NewNpc(new NpcId(1));
        var target = NewNpc(new NpcId(2));
        world.AddNpc(carrier);
        world.AddNpc(target);
        var ctx = new ExtraordinaryMechanicContext(
            world, new TickContext(world, world.Rng, world.Scheduler),
            new ExtraordinaryInvocation(1, carrier.Id, "p", target.Id), carrier, target,
            ExtraordinaryMechanicKind.Effect);
        return (ctx, carrier, target);
    }

    private static Npc NewNpc(NpcId id) => new(
        id, "npc" + id.Value, Sex.Male, WorldDate.Epoch(ScenarioRunner.DefaultCalendar),
        ScenarioRunner.DefaultCulture, new CellCoord(0, 0), motherId: null, fatherId: null,
        household: null, health: 50,
        personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
        profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
}
