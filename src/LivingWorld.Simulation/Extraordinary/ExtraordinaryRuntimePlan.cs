using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Plano inspecionável do gate de registro. Conteúdo desligado permanece fora do caminho quente.
/// </summary>
public sealed record ExtraordinaryRuntimePlan(
    IReadOnlyList<string> CarrierIds,
    IReadOnlyList<string> Events,
    IReadOnlyList<string> SystemNames)
{
    /// <summary>Valida a borda e só materializa um plano depois do cenário inteiro ser aceito.</summary>
    public static Result<ExtraordinaryRuntimePlan> Load(string json)
    {
        var loaded = ExtraordinaryScenarioLoader.Load(json);
        return loaded.IsSuccess
            ? Create(loaded.Value!)
            : Result<ExtraordinaryRuntimePlan>.Fail(loaded.Error!);
    }

    public static Result<ExtraordinaryRuntimePlan> Create(ExtraordinaryScenarioData scenario) =>
        Result<ExtraordinaryRuntimePlan>.Ok(
            new ExtraordinaryRuntimePlan(
                [],
                [],
                scenario.Enabled
                    ? [
                        ExtraordinaryStateSystem.SystemName,
                        ExtraordinaryPassiveTickSystem.SystemName,
                        DimensionPortalSystem.SystemName,
                        FaunaDominateSystem.SystemName,
                        FloraGrowthSystem.SystemName,
                    ]
                    : []));
}
