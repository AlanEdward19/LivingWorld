using LivingWorld.Domain.Extraordinary;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Extraordinary.Inheritance;

/// <summary>AD-156, 08b item 106: o índice de portadores vivos e as fontes de flora calculadas uma vez por hora são só
/// otimização. Vila padrão com os poderes de referência mais um poder que dobra o crescimento da flora num raio (portadores
/// desde a fundação, que morrem no período), 6 anos, duas seeds: o hash canônico tem de ser o mesmo gravado com o caminho
/// antigo (varredura de todos os portadores, mortos inclusive, por planta por hora; commit c279bad4). Pinados os 41 primeiros
/// dígitos hexadecimais que o xUnit mostrou na rodada do caminho antigo.</summary>
[Trait("Category", "Scenario")]
public sealed class PowerCostEquivalenceTests
{
    private const long Years = 6;

    internal static ExtraordinaryScenarioData FloraScenario()
    {
        var reference = PowerGeneticsReport.ReferenceScenario();
        var green = new PowerDescriptor("verde", "genetica", ["area:radius:5", "flora.growth-rate:2"], "Active", [],
            "Guaranteed", [], [], [], []);
        var perPower = new Dictionary<string, PowerGenetics>(reference.InheritanceRules!.PerPower!)
        {
            ["verde"] = new(AlleleFrequency: 0.15, Penetrance: 1, OnsetAgeMeanYears: 0, OnsetAgeStdDevYears: 0),
        };
        return new ExtraordinaryScenarioData(true, [.. reference.Descriptors, green], prevalence: reference.Prevalence,
            inheritanceRules: new PowerInheritanceRules(reference.InheritanceRules.Genetics, perPower));
    }

    internal static (string Hash, int DeadGreenCarriers) HashAfter(ulong seed)
    {
        var (world, clock) = ScenarioRunner.Create(seed, extraordinary: FloraScenario());
        clock.Run(world, Years * world.Calendar.HoursPerYear);
        int dead = world.ExtraordinaryCarriers.Count(c => c.PowerIds.Contains("verde") && world.FindNpc(c.CarrierId) is { IsAlive: false });
        return (WorldSnapshot.CanonicalHash(world), dead);
    }

    [Theory]
    [InlineData(1UL, "7c4ac39c6d8b1856a6685ecbdfc4f38df7839c66b")]
    [InlineData(2UL, "95318fd58f2c664cc1239f017ea56305eda004fde")]
    public void Living_carrier_index_and_hourly_flora_sources_keep_the_world_hash_of_the_old_path(ulong seed, string oldPathHashPrefix)
    {
        var (hash, dead) = HashAfter(seed);

        Assert.True(dead > 0, "nenhum portador do poder de flora morreu: o teste não passa pelo índice de vivos");
        Assert.StartsWith(oldPathHashPrefix, hash, StringComparison.Ordinal);
    }
}
