using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Extraordinary;

public sealed class ExtraordinaryHeredityTests
{
    [Fact]
    public void Acquisition_rate_uses_rate_gene_and_is_deterministic()
    {
        var highA = World(42);
        var highB = World(42);
        var low = World(42);
        AddNpc(highA, 1, new RateGene(2));
        AddNpc(highB, 1, new RateGene(2));
        AddNpc(low, 1, new RateGene(0.01));
        Schedule(highA);
        Schedule(highB);
        Schedule(low);

        var clock = new WorldClock([new ExtraordinaryStateSystem()]);
        clock.Tick(highA);
        clock.Tick(highB);
        clock.Tick(low);

        Assert.Single(highA.ExtraordinaryCarriers);
        Assert.Empty(low.ExtraordinaryCarriers);
        Assert.Equal(WorldSnapshot.CanonicalHash(highA), WorldSnapshot.CanonicalHash(highB));
    }

    [Fact]
    public void Birth_inherits_parent_rate_gene_but_not_parent_power()
    {
        var world = World(7);
        var location = new CellCoord(1, 1);
        var mother = AddNpc(world, 1, new RateGene(2), Sex.Female, location);
        var father = AddNpc(world, 2, new RateGene(2), Sex.Male, location);
        mother.Marry(father.Id);
        father.Marry(mother.Id);
        var household = new Household(new HouseholdId(1), location, mother.Id, [mother.Id, father.Id]);
        mother.JoinHousehold(household.Id);
        father.JoinHousehold(household.Id);
        world.AddHousehold(household);
        world.UpsertExtraordinaryCarrier(Carrier(mother.Id));
        world.UpsertExtraordinaryCarrier(Carrier(father.Id));

        new NatalitySystem().HandleEvent(
            world,
            new TickContext(world, world.Rng, world.Scheduler),
            new ScheduledEvent(1, world.CurrentDate.TotalHours, NatalitySystem.SystemName, "1|2|1|100"));

        var child = Assert.Single(world.Npcs, npc => npc.MotherId == mother.Id);
        Assert.InRange(child.RateGene.Value, 1.7, 2.3);
        Assert.DoesNotContain(world.ExtraordinaryCarriers, carrier => carrier.CarrierId == child.Id);
    }

    [Fact]
    public void Birth_sample_ci_contains_zero_for_power_copy_and_is_positive_for_rate_gene()
    {
        const int birthSampleTarget = 200;
        var parentPower = new double[birthSampleTarget];
        var childPower = new double[birthSampleTarget];
        var parentGene = new double[birthSampleTarget];
        var childGene = new double[birthSampleTarget];
        for (int i = 0; i < birthSampleTarget; i++)
        {
            parentPower[i] = i % 2;
            childPower[i] = 0; // integração acima prova que NatalitySystem não copia carrier.
            parentGene[i] = i % 2 == 0 ? 0.5 : 1.5;
            childGene[i] = RateGene.Inherit(
                new RateGene(parentGene[i]), new RateGene(1), new WorldRng((ulong)(i + 1))).Value;
        }

        var powerCi = Fisher95(Pearson(parentPower, childPower), birthSampleTarget);
        var geneCi = Fisher95(Pearson(parentGene, childGene), birthSampleTarget);

        Assert.True(powerCi.Low <= 0 && powerCi.High >= 0,
            $"IC95 power copy [{powerCi.Low:F3},{powerCi.High:F3}] deveria conter zero");
        Assert.True(geneCi.Low > 0,
            $"IC95 predisposição [{geneCi.Low:F3},{geneCi.High:F3}] deveria ser positivo");
    }

    private static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        double meanX = x.Average();
        double meanY = y.Average();
        double numerator = 0, sumX = 0, sumY = 0;
        for (int i = 0; i < x.Count; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;
            numerator += dx * dy;
            sumX += dx * dx;
            sumY += dy * dy;
        }
        return sumX == 0 || sumY == 0 ? 0 : numerator / Math.Sqrt(sumX * sumY);
    }

    private static (double Low, double High) Fisher95(double correlation, int sampleSize)
    {
        double bounded = Math.Clamp(correlation, -0.999999, 0.999999);
        double z = Math.Atanh(bounded);
        double margin = 1.96 / Math.Sqrt(sampleSize - 3);
        return (Math.Tanh(z - margin), Math.Tanh(z + margin));
    }

    private static WorldState World(ulong seed)
    {
        var descriptor = new PowerDescriptor(
            "latent", "scenario", ["npc.health:1"], "Triggered", [], "Guaranteed", [], [], [],
            ["rate:0.6:event:exposure"]);
        return new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.DefaultMap(seed),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]));
    }

    private static Npc AddNpc(
        WorldState world, long id, RateGene rateGene, Sex sex = Sex.Female, CellCoord? location = null)
    {
        var cell = location ?? new CellCoord(0, 0);
        var npc = new Npc(
            new NpcId(id), $"npc-{id}", sex,
            WorldDate.Epoch(ScenarioRunner.DefaultCalendar).AddYears(-20), new CultureId(1), cell,
            null, null, null, 100,
            Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            ProfessionType.None, currentLocation: cell, rateGene: rateGene);
        world.AddNpc(npc);
        return npc;
    }

    private static ExtraordinaryCarrierState Carrier(NpcId id) => new(
        id, ["latent"], false, "dormant", new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private static void Schedule(WorldState world) =>
        new TickContext(world, world.Rng, world.Scheduler).ScheduleEvent(
            1, ExtraordinaryStateSystem.SystemName, "acquire|1|latent|exposure");
}
