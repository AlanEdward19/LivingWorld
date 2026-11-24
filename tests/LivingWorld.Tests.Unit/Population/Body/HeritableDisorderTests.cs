using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Body;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Population.Body.HormonesTests;

namespace LivingWorld.Tests.Unit.Population.Body;

/// <summary>AD-139: doenças genéticas mendelianas (fundadores pela frequência, segregação, portador sem sintoma, equilíbrio
/// de Hardy-Weinberg sem seleção, efeito na mortalidade) e a contracepção que só existe se alguém a inventou.</summary>
public class HeritableDisorderTests
{
    private static Npc Transient(WorldState world, long id, uint genome)
    {
        var npc = new Npc(new NpcId(id), $"g{id}", Sex.Female, WorldDate.Epoch(world.Calendar), new CultureId(1), new CellCoord(0, 0),
            motherId: new NpcId(1), fatherId: null, household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!, profession: ProfessionType.None,
            currentLocation: new CellCoord(0, 0));
        npc.SetGenome(genome);
        npc.SetGestationAtBirth(268); // nascido no mundo: o genótipo é o guardado
        return npc;
    }

    [Fact]
    public void Founders_carry_disease_alleles_at_the_founder_frequency()
    {
        var world = HormonesTests.World();
        int carriers = 0;
        const int n = 20_000;
        for (int i = 0; i < n; i++)
        {
            var founder = new Npc(new NpcId(10_000 + i), "f", Sex.Male, WorldDate.Epoch(world.Calendar), new CultureId(1), new CellCoord(0, 0),
                null, null, null, 100, Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!, ProfessionType.None, new CellCoord(0, 0));
            if (Genome.Copies(Genome.Of(world, founder), 1) == 1) carriers++;
        }

        double q = HeritableDisorderRules.Default.Loci[1].FounderAlleleFrequency;
        Assert.InRange(carriers / (double)n, 2 * q * (1 - q) - 0.008, 2 * q * (1 - q) + 0.008);
    }

    [Fact]
    public void Two_carriers_have_a_quarter_of_children_affected_and_a_carrier_with_a_clean_partner_has_none()
    {
        var world = HormonesTests.World();
        uint carrier = 1u; // uma cópia no locus 0 (recessiva)
        var mother = Transient(world, 2, carrier);
        var carrierFather = Transient(world, 3, carrier);
        var cleanFather = Transient(world, 4, 0);
        var rng = new WorldRng(3);
        int affected = 0, affectedWithClean = 0;
        const int n = 8_000;
        for (int i = 0; i < n; i++)
        {
            if (Genome.Copies(Genome.Inherit(world, mother, carrierFather, rng), 0) == 2) affected++;
            if (Genome.Copies(Genome.Inherit(world, mother, cleanFather, rng), 0) == 2) affectedWithClean++;
        }

        Assert.InRange(affected / (double)n, 0.23, 0.27);
        Assert.Equal(0, affectedWithClean);
        Assert.False(Genome.IsAffected(HeritableDisorderRules.Default.Loci[0], 1)); // portador de recessiva não adoece
    }

    [Fact]
    public void Without_selection_the_allele_frequency_holds_across_generations()
    {
        var world = HormonesTests.World();
        var rng = new WorldRng(17);
        const int n = 4_000;
        var generation = new uint[n];
        for (int i = 0; i < n; i++)
            generation[i] = (rng.NextDouble() < 0.04 ? 1u << 2 : 0) + (rng.NextDouble() < 0.04 ? 1u << 2 : 0); // locus 1

        long id = 100;
        for (int g = 0; g < 10; g++)
        {
            var next = new uint[n];
            for (int i = 0; i < n; i++)
            {
                var m = Transient(world, id++, generation[(int)(rng.NextDouble() * n)]);
                var f = Transient(world, id++, generation[(int)(rng.NextDouble() * n)]);
                next[i] = Genome.Inherit(world, m, f, rng);
            }
            generation = next;
        }

        double q = generation.Sum(x => Genome.Copies(x, 1)) / (2.0 * n);
        Assert.InRange(q, 0.02, 0.06);
    }

    [Fact]
    public void An_affected_person_dies_at_the_locus_hazard_and_a_carrier_does_not()
    {
        var world = HormonesTests.World();
        var affected = Person(world, Sex.Male, 5);
        var carrier = Person(world, Sex.Male, 5);
        var clean = Person(world, Sex.Male, 5);
        affected.SetGenome(2u); // duas cópias no locus 0
        carrier.SetGenome(1u);
        // pais conhecidos: o genótipo é o guardado, não o do fundador
        foreach (var n in new[] { affected, carrier, clean }) n.SetGestationAtBirth(268);
        double Hazard(Npc n) => MortalitySystem.StateMultiplier(world, n, firstYear: false);

        Assert.Equal(HeritableDisorderRules.Default.Loci[0].HazardMultiplier, Hazard(affected) / Hazard(clean), 6);
        Assert.Equal(Hazard(clean), Hazard(carrier));
    }

    [Fact]
    public void Contraception_protects_only_after_someone_invented_it_and_the_city_adopted_it()
    {
        var reproduction = Certain with
        {
            Contraception = ContraceptionRules.Default with { MaxEfficacy = 1, UseShare = 1, DesiredChildren = 0 },
        };
        int Pregnancies(bool invented)
        {
            var world = HormonesTests.World(reproduction);
            int count = 0;
            for (int c = 0; c < 6; c++)
            {
                var wife = Person(world, Sex.Female, 25);
                var husband = Person(world, Sex.Male, 27);
                Home(world, wife, husband);
                Wed(wife, husband);
            }
            if (invented)
                world.AddTechnology(new Technology(new CultureId(1), ContraceptionRules.FertilityControlCategory,
                    new TechnologyVector([world.EmergenceRules.AxisMax, world.EmergenceRules.AxisMax, world.EmergenceRules.AxisMax]),
                    0, default, null, [default]));
            var natality = new NatalitySystem();
            for (int m = 0; m < 3; m++)
            {
                natality.Tick(world, Ctx(world));
                world.CurrentDate = world.CurrentDate.AddHours(world.Calendar.HoursPerMonth);
            }
            foreach (var npc in world.AliveNpcIndex.Alive) if (npc.PregnantUntil is not null) count++;
            return count;
        }

        Assert.Equal((6, 0), (Pregnancies(invented: false), Pregnancies(invented: true)));
    }

    [Fact]
    public void Puberty_and_genome_survive_the_snapshot_and_an_old_snapshot_opens_without_them()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5);
        var npc = world.AliveNpcIndex.Alive[0];
        string old = WorldSnapshot.Serialize(world);
        npc.SetPuberty(1234);
        npc.SetGenome(9u);

        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world)).FindNpc(npc.Id)!;
        var fromOld = WorldSnapshot.Deserialize(old).FindNpc(npc.Id)!;

        Assert.Equal((1234L, 9u), (back.PubertyTick!.Value, back.Genome));
        Assert.Equal(((long?)null, 0u), (fromOld.PubertyTick, fromOld.Genome));
    }
}
