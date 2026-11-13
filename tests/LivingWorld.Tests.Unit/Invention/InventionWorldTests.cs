using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Invention;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Invention;

/// <summary>AD-141: nada escrito à mão. O registro nasce vazio, todo design tem proveniência, seeds diferentes inventam coisas
/// diferentes, a física decide e o saber morre com o último que sabia. Mundo "ansioso" (gatilho e limiar baixos) para caber em
/// poucos anos; o cenário padrão usa os valores de época (medidos no relatório de 20 seeds × 100 anos).</summary>
[Trait("Category", "Scenario")]
public class InventionWorldTests
{
    internal static readonly InventionRules Eager = InventionRules.Default with { BaseAttemptChancePm = 1000, FrustrationThresholdMinutes = 30 };
    internal const int Years = 6;

    internal sealed record Observed(ulong Seed, IReadOnlyList<Design> Designs, IReadOnlyList<DesignTombstone> Tombstones,
        IReadOnlyList<KnownDesign> Known, long FirstTick, string Hash, long NextNpcId);

    /// <summary>Corrida compartilhada (ADR-0044): só valores observados saem daqui; o teste de equivalência confere o hash.</summary>
    private static readonly Lazy<Observed[]> Runs = new(() => [Run(1, Eager), Run(2, Eager)]);

    internal static Observed Run(ulong seed, InventionRules rules)
    {
        var (world, clock) = ScenarioRunner.Create(seed, inventionRules: rules);
        long firstTick = world.Invention!.Designs.Count;
        clock.Run(world, Years * world.Calendar.HoursPerYear);
        var ledger = world.Invention!;
        return new Observed(seed, ledger.Designs.ToList(), ledger.Tombstones.ToList(), ledger.Known.ToList(), firstTick, WorldSnapshot.CanonicalHash(world), world.NextNpcId);
    }

    [Fact]
    public void Registry_is_empty_at_tick_zero_in_the_code_scenario()
    {
        var (world, _) = ScenarioRunner.Create(1);

        Assert.True(world.InventionOn);
        Assert.Empty(world.Invention!.Designs);
    }

    [Fact]
    public void No_scenario_file_declares_designs()
    {
        var root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "LivingWorld.sln"))) root = Directory.GetParent(root)!.FullName;
        var files = Directory.EnumerateFiles(Path.Combine(root, "scenarios"), "*.json", SearchOption.AllDirectories).ToList();

        Assert.NotEmpty(files);
        Assert.DoesNotContain(files, f => File.ReadAllText(f).Contains("\"Designs\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_village_with_water_on_the_way_comes_to_invent_something_that_floats()
    {
        Assert.Contains(Runs.Value, r => r.Designs.Any(d => d.Origin == DesignOrigin.Invented));
    }

    [Fact]
    public void Every_design_has_provenance_who_when_and_from_what()
    {
        foreach (var run in Runs.Value)
        {
            var known = run.Designs.Select(d => d.Id).Concat(run.Tombstones.Select(t => t.DesignId)).ToHashSet();
            foreach (var design in run.Designs)
            {
                Assert.True(design.InventorId >= 0 && design.InventorId < run.NextNpcId && design.Tick > 0, $"seed {run.Seed}: design {design.Id} sem quem ou quando");
                Assert.True(design.Origin == DesignOrigin.Invented ? design.ParentId == 0 : known.Contains(design.ParentId),
                    $"seed {run.Seed}: design {design.Id} ({design.Origin}) de pai {design.ParentId} desconhecido");
                Assert.Equal(Design.IdOf(design.Parts), design.Id);
            }
        }
    }

    [Fact]
    public void Two_seeds_invent_design_sets_that_overlap_less_than_half()
    {
        var a = Runs.Value[0].Designs.Select(d => d.Id).ToHashSet();
        var b = Runs.Value[1].Designs.Select(d => d.Id).ToHashSet();
        Assert.True(a.Count > 0 && b.Count > 0, $"as duas seeds precisam inventar (A={a.Count}, B={b.Count})");

        double overlap = (double)a.Intersect(b).Count() / Math.Min(a.Count, b.Count);

        Assert.True(overlap < 0.5, $"sobreposição {overlap:P0}");
    }

    [Fact]
    public void Shared_run_is_equivalent_to_an_isolated_run()
    {
        Assert.Equal(Run(1, Eager).Hash, Runs.Value[0].Hash);
    }

    [Fact]
    public void With_wood_denser_than_water_the_world_accepts_no_solid_wooden_design()
    {
        var dense = Eager with
        {
            Materials = Eager.Materials.Select(m => m.Id == 1 ? m with { DensityKgM3 = 1100 } : m).ToList(),
        };
        var run = Run(1, dense);

        Assert.DoesNotContain(run.Designs, d => d.Parts.All(p => p.Material == 1 && p.Coating == 0 && !p.IsHollow));
    }

    [Fact]
    public void On_foot_water_on_the_way_costs_more_than_in_a_world_without_invention_and_a_working_craft_costs_less()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var (plain, _) = ScenarioRunner.Create(1, inventionRules: InventionRules.Disabled);
        var npc = world.Npcs.First(n => n.IsAlive && n.Household is not null);
        var (from, to) = WaterTrip(world);

        long none = Mobility.TravelTicks(plain, plain.FindNpc(npc.Id)!, from, to, 1.0);
        long foot = Mobility.TravelTicks(world, npc, from, to, 1.0);
        int water = WaterCrossing.CellsBetween(world.Map, from, to);
        var design = GiveCraft(world, npc);
        int speed = world.Invention!.BestCraftSpeed(npc.Household!.Value.Value, world.InventionRules!);
        long craft = Mobility.TravelTicks(world, npc, from, to, 1.0);

        Assert.True(foot > none && craft <= foot && speed > 0, $"sem invenção {none}, a pé {foot}, embarcado {craft} ({design.Id})");
        Assert.True(WaterCrossing.ExtraCost(world.Map, world.InventionRules!, water, speed) < WaterCrossing.ExtraCost(world.Map, world.InventionRules!, water, 0));
    }

    [Fact]
    public void Walking_through_water_is_lived_as_frustration_and_a_craft_at_home_spares_it()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var npc = world.Npcs.First(n => n.IsAlive && n.Household is not null && n.AgeYears(world.CurrentDate) >= 20);
        var (from, to) = WaterTrip(world);

        InventionIntake.OnTravelArrived(world, npc, from, to, 1);
        int felt = world.Invention!.Minds[world.Invention.MindIndex(npc.Id.Value)].FrustrationMinutes;
        GiveCraft(world, npc);
        InventionIntake.OnTravelArrived(world, npc, from, to, 2);

        Assert.True(felt > 0);
        Assert.Equal(felt, world.Invention.Minds[world.Invention.MindIndex(npc.Id.Value)].FrustrationMinutes);
    }

    [Fact]
    public void The_last_knower_dying_buries_the_design_unless_an_item_survives()
    {
        var (world, _) = ScenarioRunner.Create(1);
        var npc = world.Npcs.First(n => n.IsAlive && n.Household is not null);
        var ledger = world.Invention!;
        var kept = GiveCraft(world, npc);
        var lonely = Design.Create([kept.Parts[0] with { LengthMm = (short)(kept.Parts[0].LengthMm - 100) }], npc.Id.Value, 5, 0, 0, DesignOrigin.Invented);
        ledger.AddDesign(lonely);
        ledger.Learn(new KnownDesign(npc.Id.Value, lonely.Id, 1000, DesignOrigin.Invented, 5));

        InventionIntake.Forget(world, npc.Id.Value, 100);

        Assert.Null(ledger.FindDesign(lonely.Id));
        Assert.Contains(ledger.Tombstones, t => t.DesignId == lonely.Id && t.InventorId == npc.Id.Value && t.LostTick == 100);
        Assert.NotNull(ledger.FindDesign(kept.Id)); // a instância que sobrou guarda o design para quem a copiar
    }

    [Fact]
    public void Disabled_rules_leave_no_trace_in_the_snapshot()
    {
        var (world, _) = ScenarioRunner.Create(1, inventionRules: InventionRules.Disabled);

        string json = WorldSnapshot.Serialize(world);
        Assert.DoesNotContain("\"InventionRules\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Invention\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_world_with_designs_reopens_from_the_snapshot_and_goes_on_identically()
    {
        var (world, clock) = ScenarioRunner.Create(1, inventionRules: Eager);
        clock.Run(world, 2 * world.Calendar.HoursPerYear);
        var reopened = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));

        clock.Run(world, world.Calendar.HoursPerYear);
        new WorldClock(ScenarioRunner.DefaultSystems(), clock.MaxIterationsPerTick).Run(reopened, world.Calendar.HoursPerYear);

        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(reopened));
    }

    /// <summary>Um trajeto do mapa cuja reta atravessa água.</summary>
    private static (CellCoord From, CellCoord To) WaterTrip(WorldState world)
    {
        var cells = world.Map.Cells.Where(c => !c.HasWater).Select(c => c.Coord).OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
        foreach (var a in cells.Take(200))
            foreach (var b in cells.Skip(200).Take(400))
                if (WaterCrossing.CellsBetween(world.Map, a, b) >= 5) return (a, b);
        throw new InvalidOperationException("mapa sem água no caminho");
    }

    /// <summary>Uma embarcação que funciona, composta pelos operadores de busca a partir da peça natural que boia, na casa do NPC.</summary>
    private static Design GiveCraft(WorldState world, Npc npc)
    {
        var rules = world.InventionRules!;
        var wood = rules.Materials.First(m => !m.CoatingOnly && m.FloatsIn(rules.WaterDensityKgM3) && m.MaxThicknessMm >= 400);
        var rng = new WorldRng(3);
        IReadOnlyList<DesignPart> seed = [DesignSearch.NaturalPiece(wood)];
        for (int i = 0; i < 5000; i++)
        {
            var v = DesignSearch.Variant(seed, [], [wood], FailureMode.Capsizes, rng);
            if (!PhysicsEvaluator.Evaluate(v, rules, rules.ReferenceRiderKg).Works) { if (i % 7 == 0) seed = v; continue; }
            var design = Design.Create(v, npc.Id.Value, 1, 0, 0, DesignOrigin.Invented);
            world.Invention!.AddDesign(design);
            world.Invention.Learn(new KnownDesign(npc.Id.Value, design.Id, 1000, DesignOrigin.Invented, 1));
            world.Invention.Items.Add(new CraftedItem(world.Invention.NextItemId++, design.Id, npc.Household!.Value.Value, npc.City, 1, long.MaxValue, 1000));
            world.Invention.Touch();
            return design;
        }
        throw new InvalidOperationException("a busca não achou nada que funcione");
    }
}
