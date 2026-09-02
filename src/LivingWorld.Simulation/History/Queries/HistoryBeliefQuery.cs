using LivingWorld.Domain.History.Distortion;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.History.Distortion;

namespace LivingWorld.Simulation.History.Queries;

/// <summary>Único ponto de acesso à crença de um NPC ou comunidade (Fase 10, T15,
/// HIST-16) — resolve o <see cref="ReportState"/> vigente no cânone e materializa o
/// <see cref="DistortedReport"/> sob demanda, nunca o fato bruto.</summary>
public static class HistoryBeliefQuery
{
    public const string NeverHeardError = "esta comunidade nunca ouviu falar deste fato";

    public static Result<DistortedReport> BeliefOf(WorldState world, NpcId believerId, FactId originFactId)
    {
        var community = ResolveCommunity(world, believerId);
        if (!community.IsSuccess)
            return Result<DistortedReport>.Fail(community.Error!);

        return BeliefOf(world, community.Value!, originFactId);
    }

    public static Result<DistortedReport> BeliefOf(WorldState world, CityId community, FactId originFactId)
    {
        if (!world.HistoryRules.Enabled)
            return Result<DistortedReport>.Fail("history_disabled");

        var city = world.FindCity(community);
        if (city is null)
            return Result<DistortedReport>.Fail("City: não existe");

        var report = city.CanonSlots.FirstOrDefault(r => r.OriginFactId == originFactId);
        if (report is null)
            return Result<DistortedReport>.Fail(NeverHeardError);

        var origin = world.FindFact(originFactId);
        if (origin is null)
            return Result<DistortedReport>.Fail("Fact: não existe");

        var distorted = DistortionEngine.Materialize(
            report, origin, world.HistoryRules, world.Rng, world);
        return Result<DistortedReport>.Ok(distorted);
    }

    private static Result<CityId> ResolveCommunity(WorldState world, NpcId believerId)
    {
        var npc = world.FindNpc(believerId);
        if (npc is not null)
            return Result<CityId>.Ok(npc.City);

        // Pool agregado (AD-068): o id endereçável é o próximo que MaterializeOne emitiria.
        if (believerId.Value == world.NextNpcId)
        {
            var city = world.Cities.FirstOrDefault(c => c.AggregatePool.Count > 0);
            if (city is not null)
                return Result<CityId>.Ok(city.Id);
        }

        return Result<CityId>.Fail("Npc: não existe");
    }
}
