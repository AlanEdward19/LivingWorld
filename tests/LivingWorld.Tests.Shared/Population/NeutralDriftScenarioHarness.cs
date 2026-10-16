using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Shared.Population;

/// <summary>Fase 7, T21 (FAM-23, FAM-25): braço de deriva neutra sobre o cenário default — troca
/// <see cref="FamilyRules.NeutralDriftEnabled"/> (escolha de parceiro) e
/// <see cref="FamilyRules.VitalityMortalitySelectionEnabled"/> (seleção de mortalidade por
/// Vitality) juntas via <see cref="ScenarioRunner.Create"/>, nunca duplica montagem de cenário
/// (AD-059). As duas flags eram uma só até AD-065 — deriva neutra "de verdade" precisa desligar
/// os dois canais de seleção (mate-choice E mortalidade), senão o controle não isola o efeito
/// certo (FAM-32/AD-064).</summary>
public static class NeutralDriftScenarioHarness
{
    public static (WorldState World, WorldClock Clock) Create(
        ulong seed, bool neutralDriftEnabled, int initialPopulation = ScenarioRunner.DefaultInitialPopulation)
    {
        var familyRules = ScenarioRunner.DefaultFamilyRules with
        {
            NeutralDriftEnabled = neutralDriftEnabled,
            VitalityMortalitySelectionEnabled = !neutralDriftEnabled,
        };
        return ScenarioRunner.Create(seed, initialPopulation: initialPopulation, familyRules: familyRules);
    }
}

public class NeutralDriftScenarioHarnessTests
{
    [Fact]
    public void Neutral_drift_enabled_produces_a_different_world_hash_than_default_on_same_seed()
    {
        const ulong seed = 42;
        const long ticks = 365 * 5;

        var (worldDefault, clockDefault) = ScenarioRunner.Create(seed);
        clockDefault.Run(worldDefault, ticks);

        var (worldDrift, clockDrift) = NeutralDriftScenarioHarness.Create(seed, neutralDriftEnabled: true);
        clockDrift.Run(worldDrift, ticks);

        Assert.NotEqual(
            WorldSnapshot.CanonicalHash(worldDefault),
            WorldSnapshot.CanonicalHash(worldDrift));
    }

    [Fact]
    public void Neutral_drift_disabled_matches_default_scenario_runner()
    {
        const ulong seed = 99;
        const long ticks = 365;

        var (worldDefault, clockDefault) = ScenarioRunner.Create(seed);
        clockDefault.Run(worldDefault, ticks);

        var (worldExplicitOff, clockExplicitOff) = NeutralDriftScenarioHarness.Create(seed, neutralDriftEnabled: false);
        clockExplicitOff.Run(worldExplicitOff, ticks);

        Assert.Equal(
            WorldSnapshot.CanonicalHash(worldDefault),
            WorldSnapshot.CanonicalHash(worldExplicitOff));
    }
}
