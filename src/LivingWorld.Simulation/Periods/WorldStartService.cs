using System.Text.Json.Nodes;
using LivingWorld.Domain;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Simulation.Periods;

/// <summary>Bootstrap de mundo a partir de um template registrado (Fase 13, T6): resolve o
/// payload por <see cref="PeriodId"/>, sobrepõe a <c>Seed</c> pedida e roda o mesmo pipeline de
/// <see cref="ScenarioLoaderV2"/> dos templates base — nenhum caminho separado pra período
/// cadastrado via API vs. período de arquivo.</summary>
public static class WorldStartService
{
    public static Result<(WorldState World, WorldClock Clock)> Start(
        Func<string, string?> findLatestTemplatePayload, string periodId, ulong seed, int maxIterationsPerTick = 1000)
    {
        var payloadJson = findLatestTemplatePayload(periodId);
        if (payloadJson is null)
            return Result<(WorldState, WorldClock)>.Fail($"PeriodId {periodId} não encontrado");

        var root = JsonNode.Parse(payloadJson)!.AsObject();
        root["Seed"] = seed;

        return ScenarioLoaderV2.LoadWorld(root.ToJsonString(), maxIterationsPerTick);
    }
}
