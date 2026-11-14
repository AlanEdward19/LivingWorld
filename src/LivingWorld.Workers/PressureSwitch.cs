using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Medição com os freios preventivos (AD-146): <c>LW_PRESSURE</c> vazio = padrão (ligado desde o AD-148); <c>off</c> = desligado; <c>all</c> = <see
/// cref="PopulationPressureRules.Measured"/>; ou uma lista com vírgula das chaves que ficam ligadas (<c>means</c>, <c>first</c>,
/// <c>kin</c>, <c>alms</c>, <c>topup</c>, <c>emig</c>), o resto desligado. Só para os relatórios, não muda o cenário padrão.</summary>
public static class PressureSwitch
{
    public static FamilyRules? FamilyRules()
    {
        var raw = Environment.GetEnvironmentVariable("LW_PRESSURE");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw == "off") return ScenarioRunner.DefaultFamilyRules with { Pressure = null }; // AD-148: o padrão agora é ligado
        if (raw == "all") return ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured };
        var on = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var m = PopulationPressureRules.Measured;
        return ScenarioRunner.DefaultFamilyRules with
        {
            Pressure = m with
            {
                MarriageMeansWeight = on.Contains("means") ? m.MarriageMeansWeight : 0,
                FirstBirthMeansWeight = on.Contains("first") ? m.FirstBirthMeansWeight : 0,
                KinFoodAid = on.Contains("kin"),
                RationedAlms = on.Contains("alms"),
                TopUpPurchases = on.Contains("topup"),
                Emigration = on.Contains("emig") ? m.Emigration : EmigrationRules.Disabled,
            },
        };
    }
}
