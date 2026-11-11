using System.Text.Json.Nodes;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Tests.Unit.Observation;

namespace LivingWorld.Tests.Unit.Population.Lifecycle;

/// <summary>AD-130: o estado novo (gravidez, histórico reprodutivo, gestação ao nascer, deslocamento de vida) sobrevive ao
/// snapshot; snapshot e cenário JSON sem os campos abrem e herdam o padrão de época.</summary>
public class ReproductionSnapshotTests
{
    [Fact]
    public void Pregnancy_and_reproductive_history_survive_the_snapshot_round_trip()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5);
        var woman = world.AliveNpcIndex.Alive.First(n => n.Sex == LivingWorld.Domain.Population.Sex.Female);
        woman.SetReproduction(new ReproductiveState(Parity: 2, LastBirthTick: 10, InfecundUntilTick: 400,
            Pregnancy: new PregnancyState(20, 6500, 3, "FM", 268, EndsInLoss: false)));
        woman.SetGestationAtBirth(230);
        woman.ShiftLifespanOffset(8640);

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));
        var back = rehydrated.FindNpc(woman.Id)!;

        Assert.Equal((woman.Reproduction, 230, 8640L), (back.Reproduction, back.GestationAtBirthDays!.Value, back.LifespanOffsetHours));
        Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(rehydrated));
    }

    [Fact]
    public void A_snapshot_without_the_new_fields_opens_with_the_period_default()
    {
        var (world, _) = ScenarioRunner.Create(seed: 6);
        string json = WorldSnapshot.Serialize(world);
        Assert.DoesNotContain("\"Reproduction\"", json, StringComparison.Ordinal); // o formato antigo, sem campo novo

        var rehydrated = WorldSnapshot.Deserialize(json);

        Assert.Same(ReproductionRules.Default, rehydrated.PopulationRules.Fertility);
    }

    [Fact]
    public void A_json_scenario_inherits_the_period_reproduction_without_declaring_it_and_may_override_it()
    {
        var root = JsonNode.Parse(ObservatoryFixture.DefaultScenarioJson())!.AsObject();
        var inherited = PopulationScenarioLoader.Load(root.ToJsonString()).Value!;
        root["Reproduction"] = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(ReproductionRules.Default with { TwinRate = 0.02 }));
        var overridden = PopulationScenarioLoader.Load(root.ToJsonString());

        Assert.Same(ReproductionRules.Default, inherited.Rules.Fertility);
        Assert.Equal(0.02, overridden.Value!.Rules.Fertility.TwinRate);
    }
}
