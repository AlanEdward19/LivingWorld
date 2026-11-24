using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Inheritance;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Population.Body;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Extraordinary.Inheritance;

/// <summary>AD-156: fundadores em Hardy-Weinberg, penetrância, idade de início, expressividade e o gatilho que só desperta
/// quem tem o genótipo. Amostras grandes; tolerâncias declaradas em cada teste.</summary>
public sealed class PowerGeneticsPhenotypeTests
{
    private static WorldState World(PowerGenetics genetics, double prevalence = 0, string[]? acquisition = null)
    {
        var power = new PowerDescriptor("p", "genetica", ["npc.health:1"], "Active", [], "Guaranteed", [], [], [],
            acquisition ?? []);
        return new WorldState(
            ScenarioRunner.DefaultCalendar, 42, ScenarioRunner.DefaultMap(42), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [power], prevalence: prevalence,
                inheritanceRules: new PowerInheritanceRules(genetics)));
    }

    private static Npc Born(long id, ulong genome, Sex sex = Sex.Female) =>
        PowerInheritanceNatalityTests.BornAdult(new NpcId(id), sex, genome);

    [Fact]
    public void Founders_follow_hardy_weinberg_for_the_scenario_allele_frequency()
    {
        var world = World(new PowerGenetics(AlleleFrequency: 0.3));
        var counts = new double[3];
        const int founders = 20000;
        for (int i = 1; i <= founders; i++)
            counts[PowerGenome.Copies(PowerGenome.Founder(world, new NpcId(i), Sex.Female), 0)]++;

        // p² = 0,49, 2pq = 0,42, q² = 0,09; ±0,015 (mais de 4 desvios-padrão em 20.000)
        Assert.All(new[] { counts[0] / founders - 0.49, counts[1] / founders - 0.42, counts[2] / founders - 0.09 },
            d => Assert.InRange(d, -0.015, 0.015));
    }

    [Fact]
    public void Penetrance_is_the_fraction_of_genotype_holders_who_manifest()
    {
        var world = World(new PowerGenetics(Penetrance: 0.6));
        var locus = PowerGenome.Layout(world.Extraordinary)[0];

        double manifest = Enumerable.Range(1, 4000).Count(i => PowerGeneticsMechanic.Expresses(world, Born(i, 1), locus)) / 4000.0;

        Assert.InRange(manifest, 0.55, 0.65);
    }

    [Fact]
    public void Onset_age_centers_on_the_declared_puberty_curve()
    {
        var world = World(new PowerGenetics(OnsetAgeMeanYears: 13, OnsetAgeStdDevYears: 1.5));
        var locus = PowerGenome.Layout(world.Extraordinary)[0];
        var ages = Enumerable.Range(1, 4000).Select(i => PowerGeneticsMechanic.OnsetAgeYears(world, Born(i, 1), locus)).ToList();

        Assert.InRange(ages.Average(), 12.9, 13.1); // erro-padrão 1,5/√4000 ≈ 0,024
        Assert.InRange(ages.Min(), 13 - 3.5 * 1.5, 13);
    }

    [Fact]
    public void Onset_by_default_comes_at_each_npcs_own_puberty()
    {
        // AD-162: sem média declarada, o poder aparece na puberdade do próprio NPC (menarca média 15 no mundo, não 13)
        var world = World(new PowerGenetics());
        var locus = PowerGenome.Layout(world.Extraordinary)[0];
        var npcs = Enumerable.Range(1, 2000).Select(i => Born(i, 1)).ToList();

        Assert.All(npcs, n => Assert.Equal(Endocrine.PubertyAgeYears(world, n), PowerGeneticsMechanic.OnsetAgeYears(world, n, locus)));
        Assert.InRange(npcs.Average(n => PowerGeneticsMechanic.OnsetAgeYears(world, n, locus)), 14.8, 15.2);
    }

    [Fact]
    public void A_power_tied_to_puberty_waits_while_the_npc_has_not_reached_it()
    {
        var world = World(new PowerGenetics(Penetrance: 1));
        var child = new Npc(new NpcId(1), "crianca", Sex.Female, WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-8),
            ScenarioRunner.DefaultCulture, new LivingWorld.Domain.Geography.CellCoord(0, 0), motherId: null, fatherId: null,
            household: new HouseholdId(1), health: 100, personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new LivingWorld.Domain.Geography.CellCoord(0, 0));
        child.SetGestationAtBirth(280);
        child.SetPowerGenome(1);
        world.AddNpc(child);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        new ExtraordinaryStateSystem().HandleEvent(world, ctx,
            ctx.ScheduleEvent(1, ExtraordinaryStateSystem.SystemName, $"{PowerGeneticsMechanic.OnsetPrefix}|1|p"));

        Assert.Null(world.CarrierOf(child.Id));
    }

    [Fact]
    public void Homozygous_dominant_expresses_the_power_more_strongly_than_heterozygous_on_average()
    {
        var world = World(new PowerGenetics(Penetrance: 1));
        double het = Enumerable.Range(1, 2000).Average(i => PowerGeneticsMechanic.Intensity(world, Born(i, 1), "p"));
        double hom = Enumerable.Range(1, 2000).Average(i => PowerGeneticsMechanic.Intensity(world, Born(i, 2), "p"));

        Assert.InRange(het, 0.98, 1.02); // erro-padrão 0,15/√2000 ≈ 0,003
        Assert.InRange(hom, 1.23, 1.27);
    }

    [Fact]
    public void Power_without_the_genotype_keeps_typical_intensity()
    {
        var world = World(new PowerGenetics(Penetrance: 1));

        Assert.Equal(1.0, PowerGeneticsMechanic.Intensity(world, Born(1, 0), "p"));
    }

    [Fact]
    public void Environmental_trigger_awakens_only_genotype_holders_when_the_power_requires_it()
    {
        var world = World(new PowerGenetics(Penetrance: 0, TriggerRequiresGenotype: true), acquisition: ["event:exposure"]);
        world.AddNpc(Born(1, 1));
        world.AddNpc(Born(2, 0));
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var system = new ExtraordinaryStateSystem();

        system.HandleEvent(world, ctx, ctx.ScheduleEvent(1, ExtraordinaryStateSystem.SystemName, "acquire|1|p|exposure"));
        system.HandleEvent(world, ctx, ctx.ScheduleEvent(1, ExtraordinaryStateSystem.SystemName, "acquire|2|p|exposure"));

        Assert.Equal((true, false), (world.CarrierOf(new NpcId(1)) is not null, world.CarrierOf(new NpcId(2)) is not null));
    }
}
