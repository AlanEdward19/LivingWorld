using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population;

/// <summary>personality-heredity: o traço do filho mistura a média dos pais com sorteio próprio; irmãos se parecem mais do que
/// estranhos; com peso 0 o resultado é o sorteio independente de antes.</summary>
public class PersonalityInheritanceTests
{
    private static WorldRng Stream(ulong seed, long id) => new WorldRngRegistry(seed).StreamFor("personality", id);

    private static int[] Traits(Personality p) =>
    [
        p.Extroversion, p.Agreeableness, p.Conscientiousness, p.EmotionalStability, p.Openness,
        p.Ambition, p.Loyalty, p.Altruism, p.Impulsivity, p.RiskAversion,
    ];

    private static double Distance(Personality a, Personality b) =>
        Math.Sqrt(Traits(a).Zip(Traits(b), (x, y) => (double)(x - y) * (x - y)).Sum());

    [Fact]
    public void With_weight_zero_the_child_is_exactly_the_independent_roll_of_before()
    {
        var mother = Personality.RollFrom(Stream(1, 1));
        var father = Personality.RollFrom(Stream(1, 2));

        for (long id = 10; id < 60; id++)
            Assert.Equal(Personality.RollFrom(Stream(9, id)), Personality.Inherit(mother, father, Stream(9, id), heritability: 0.0));
    }

    [Fact]
    public void Every_trait_stays_in_range_over_a_thousand_births_in_twenty_seeds()
    {
        for (ulong seed = 1; seed <= 20; seed++)
            for (long id = 0; id < 50; id++)
            {
                var mother = Personality.RollFrom(Stream(seed, 1000 + id));
                var father = Personality.RollFrom(Stream(seed, 2000 + id));
                var child = Personality.Inherit(mother, father, Stream(seed, id), heritability: 0.7);

                Assert.All(Traits(child), trait => Assert.InRange(trait, 0, 100));
            }

        // pais nos extremos: a mistura é cortada em [0,100]
        var low = Personality.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 0).Value!;
        var high = Personality.Create(100, 100, 100, 100, 100, 100, 100, 100, 100, 100).Value!;
        for (long id = 0; id < 200; id++)
        {
            Assert.All(Traits(Personality.Inherit(low, low, Stream(3, id), 1.0)), trait => Assert.InRange(trait, 0, 100));
            Assert.All(Traits(Personality.Inherit(high, high, Stream(3, id), 1.0)), trait => Assert.InRange(trait, 0, 100));
        }
    }

    [Fact]
    public void The_same_birth_gives_the_same_personality()
    {
        var mother = Personality.RollFrom(Stream(5, 1));
        var father = Personality.RollFrom(Stream(5, 2));

        Assert.Equal(
            Personality.Inherit(mother, father, Stream(7, 42), heritability: 0.7),
            Personality.Inherit(mother, father, Stream(7, 42), heritability: 0.7));
    }

    [Fact]
    public void Siblings_are_closer_than_strangers_with_a_z_of_at_least_three_over_two_hundred_pairs()
    {
        const int pairs = 200;
        var parents = Enumerable.Range(0, pairs + 1)
            .Select(i => (Mother: Personality.RollFrom(Stream(11, 10_000 + i)), Father: Personality.RollFrom(Stream(11, 20_000 + i))))
            .ToList();
        var first = new List<Personality>();
        var second = new List<Personality>();
        for (int i = 0; i <= pairs; i++)
        {
            first.Add(Personality.Inherit(parents[i].Mother, parents[i].Father, Stream(11, 2 * i), 0.7));
            second.Add(Personality.Inherit(parents[i].Mother, parents[i].Father, Stream(11, 2 * i + 1), 0.7));
        }

        var sibling = Enumerable.Range(0, pairs).Select(i => Distance(first[i], second[i])).ToArray();
        var stranger = Enumerable.Range(0, pairs).Select(i => Distance(first[i], second[i + 1])).ToArray();

        double meanSibling = sibling.Average(), meanStranger = stranger.Average();
        double se = Math.Sqrt(Variance(sibling) / pairs + Variance(stranger) / pairs);
        double z = (meanStranger - meanSibling) / se;

        Assert.True(meanSibling < meanStranger, $"irmãos {meanSibling:F1} contra estranhos {meanStranger:F1}");
        Assert.True(z >= 3, $"z = {z:F2} (irmãos {meanSibling:F1}, estranhos {meanStranger:F1})");
    }

    [Fact]
    public void Without_inheritance_siblings_are_not_closer_than_strangers()
    {
        const int pairs = 200;
        var parents = Enumerable.Range(0, pairs + 1)
            .Select(i => (Mother: Personality.RollFrom(Stream(11, 10_000 + i)), Father: Personality.RollFrom(Stream(11, 20_000 + i))))
            .ToList();
        var first = Enumerable.Range(0, pairs + 1).Select(i => Personality.Inherit(parents[i].Mother, parents[i].Father, Stream(11, 2 * i), 0.0)).ToList();
        var second = Enumerable.Range(0, pairs + 1).Select(i => Personality.Inherit(parents[i].Mother, parents[i].Father, Stream(11, 2 * i + 1), 0.0)).ToList();

        double meanSibling = Enumerable.Range(0, pairs).Average(i => Distance(first[i], second[i]));
        double meanStranger = Enumerable.Range(0, pairs).Average(i => Distance(first[i], second[i + 1]));

        Assert.True(Math.Abs(meanSibling - meanStranger) < 8, $"controle: irmãos {meanSibling:F1}, estranhos {meanStranger:F1}");
    }

    [Fact]
    public void A_weight_outside_zero_to_one_is_rejected_by_the_family_rules()
    {
        var deltas = LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultFamilyRules.RelationshipDeltas;
        FamilyRulesResult(deltas, -0.1);
        FamilyRulesResult(deltas, 1.1);
        FamilyRulesResult(deltas, double.NaN);

        static void FamilyRulesResult(IReadOnlyDictionary<(LivingWorld.Domain.Population.Family.RelationshipEventType, LivingWorld.Domain.Population.Family.RelationshipAxis), double> deltas, double weight)
        {
            var result = LivingWorld.Domain.Population.Family.FamilyRules.Create(
                deltas, 0.25, 30, 50, new Dictionary<LivingWorld.Domain.Population.Family.AttractionFactor, double>(), 0.5, 10,
                new Dictionary<int, long>(), 0, 0, new Dictionary<int, long>(), 0, 0, 0.5, 0.5, 0, 0, 0.2, false, false, true,
                personalityHeritability: weight);
            Assert.False(result.IsSuccess);
        }
    }

    private static double Variance(IReadOnlyCollection<double> values)
    {
        double mean = values.Average();
        return values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1);
    }

    [Fact]
    public void The_population_variance_of_a_trait_is_preserved_across_generations()
    {
        // 6 gerações com pareamento aleatório e h = 0,7: a variância fica perto da de um sorteio independente (~850)
        var generation = Enumerable.Range(0, 400).Select(i => Personality.RollFrom(Stream(31, i))).ToList();
        double first = Variance(generation.Select(p => (double)p.Ambition).ToArray());
        for (int g = 0; g < 6; g++)
        {
            var next = new List<Personality>();
            for (int i = 0; i < 400; i++)
            {
                var mother = generation[(i * 7 + g) % 400];
                var father = generation[(i * 13 + 5 * g + 1) % 400];
                next.Add(Personality.Inherit(mother, father, Stream(31, 1000 * (g + 1) + i), 0.7));
            }

            generation = next;
        }

        double last = Variance(generation.Select(p => (double)p.Ambition).ToArray());
        Assert.InRange(last / first, 0.8, 1.2);
    }

    [Fact]
    public void A_child_with_an_unknown_father_inherits_from_the_mother_and_stays_in_range()
    {
        var mother = Personality.Create(90, 90, 90, 90, 90, 90, 90, 90, 90, 90).Value!;

        var children = Enumerable.Range(0, 300).Select(i => Personality.Inherit(mother, mother, Stream(41, i), 0.7)).ToList();

        Assert.All(children, c => Assert.All(Traits(c), t => Assert.InRange(t, 0, 100)));
        Assert.True(children.Average(c => c.Ambition) > 60, "a média dos filhos de uma mãe nos 90 fica acima do meio");
    }

    [Fact]
    public void The_scenario_weight_survives_a_snapshot_round_trip()
    {
        var (world, _) = LivingWorld.Simulation.Scenarios.ScenarioRunner.Create(seed: 3, initialPopulation: 5);

        var restored = LivingWorld.Simulation.Core.WorldSnapshot.Deserialize(LivingWorld.Simulation.Core.WorldSnapshot.Serialize(world));

        Assert.Equal(0.7, restored.FamilyRules.PersonalityHeritability);
    }
}
