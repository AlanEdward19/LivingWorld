using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Inheritance;

/// <summary>AD-156, 08b item 106: o índice de portadores vivos tira o morto, e a flora volta ao caminho de poderes desligados
/// (salto adiado) quando o último portador vivo de poder de flora morre.</summary>
public sealed class LivingCarrierIndexTests
{
    private static (WorldState World, TickContext Ctx, Npc Green, Npc Plain) Setup()
    {
        var green = new PowerDescriptor("verde", "genetica", ["area:radius:5", "flora.growth-rate:2"], "Active", [], "Guaranteed",
            [], [], [], []);
        var plain = new PowerDescriptor("p", "genetica", ["npc.health:1"], "Active", [], "Guaranteed", [], [], [], []);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, extraordinary: new ExtraordinaryScenarioData(true, [green, plain]));
        var a = PowerInheritanceNatalityTests.BornAdult(new NpcId(1), Sex.Female, 0);
        var b = PowerInheritanceNatalityTests.BornAdult(new NpcId(2), Sex.Male, 0);
        world.AddNpc(a);
        world.AddNpc(b);
        world.UpsertExtraordinaryCarrier(Carrier(a.Id, "verde"));
        world.UpsertExtraordinaryCarrier(Carrier(b.Id, "p"));
        return (world, new TickContext(world, world.Rng, world.Scheduler), a, b);
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id, string power) =>
        new(id, [power], true, "manifested", new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    [Fact]
    public void Dead_carrier_leaves_the_living_index_but_stays_in_the_canonical_list()
    {
        var (world, ctx, green, _) = Setup();

        NpcDeath.Apply(world, ctx, green, WorldEventKind.Death);

        Assert.Equal(("2", 2), (string.Join(',', world.LivingCarriers().Select(c => c.Npc.Id.Value)), world.ExtraordinaryCarriers.Count));
    }

    [Fact]
    public void Flora_goes_back_to_the_powers_off_path_when_the_last_living_flora_carrier_dies()
    {
        var (world, ctx, green, _) = Setup();
        bool before = FloraMechanic.PowersCanTouchFlora(world);

        NpcDeath.Apply(world, ctx, green, WorldEventKind.Death);

        Assert.Equal((true, false), (before, FloraMechanic.PowersCanTouchFlora(world)));
    }

    [Fact]
    public void The_living_index_matches_a_full_scan_of_the_carriers_every_month_of_a_village_with_deaths_and_births()
    {
        // AD-162 (08b item 107): o índice guardado contra a varredura completa filtrada por vivos, no mesmo mundo, mês a mês
        var (world, clock) = ScenarioRunner.Create(7, extraordinary: PowerGeneticsReport.ReferenceScenario());
        var mismatches = new List<string>();
        for (int month = 1; month <= 24; month++)
        {
            clock.Run(world, world.Calendar.HoursPerMonth);
            string index = string.Join(',', world.LivingCarriers().Select(c => c.Npc.Id.Value));
            string scan = string.Join(',', world.ExtraordinaryCarriers.Where(c => world.FindNpc(c.CarrierId) is { IsAlive: true })
                .Select(c => c.CarrierId.Value));
            if (index != scan) mismatches.Add($"mes {month}: indice [{index}] varredura [{scan}]");
        }

        int deadCarriers = world.ExtraordinaryCarriers.Count(c => world.FindNpc(c.CarrierId) is not { IsAlive: true });
        Assert.True(deadCarriers > 0, "nenhum portador morreu: o teste não exercitou a saída do índice");
        Assert.Empty(mismatches);
    }
}
