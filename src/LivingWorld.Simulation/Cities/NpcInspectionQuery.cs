using LivingWorld.Domain;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.History;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION: design.md pede este tipo em src/LivingWorld.Domain/Cities/ — mesmo
// SPEC_DEVIATION já registrado em CityPopulationQuery.cs: LivingWorld.Domain não referencia
// LivingWorld.Simulation, e WorldState só existe em Simulation. Vive aqui.

/// <summary>Único ponto de consulta de inspeção de NPC (Fase 8, T14, CITY-06). <see
/// cref="Inspect"/> (Fase 15.1, T49/backend-gaps.md G9) é leitura pura — nunca materializa, nunca
/// muta hash/pool/tick/eventos; falha para um id ainda anônimo no pool agregado (não existe DTO
/// individual pra devolver — só a coleta de <see cref="AggregatePopulationPool"/>, sem identidade
/// própria até materializar). <see cref="MaterializeAndInspect"/> é o comando explícito e
/// nomeado que resolve isso, separado do GET.</summary>
public static class NpcInspectionQuery
{
    /// <summary>Leitura pura (T49): idempotente, chamável quantas vezes quiser sem efeito
    /// colateral — mesmo NPC vivo/arquivado devolve sempre o mesmo DTO.</summary>
    public static Result<NpcInspectionDto> Inspect(WorldState world, NpcId id)
    {
        var npc = world.FindNpc(id);
        if (npc is not null && npc.IsAlive)
            return Result<NpcInspectionDto>.Ok(FromLiveNpc(world, npc));

        if (world.ColdArchive.Lookup(id.Value) is { } summary)
            return Result<NpcInspectionDto>.Ok(FromNpcSummary(summary));

        return Result<NpcInspectionDto>.Fail("Npc: não existe, está morto sem registro arquivado, ou ainda não foi materializado");
    }

    /// <summary>Comando explícito (T49): materializa sob demanda (<see
    /// cref="MaterializationSystem.EnsureMaterialized"/>, mesmas invariantes de sempre — só muta
    /// quando o id está pendente no pool agregado) e então devolve o mesmo DTO de <see
    /// cref="Inspect"/>. Nunca chamado implicitamente por um GET.</summary>
    public static Result<NpcInspectionDto> MaterializeAndInspect(WorldState world, NpcId id)
    {
        var ensured = MaterializationSystem.EnsureMaterialized(world, id);
        if (!ensured.IsSuccess)
        {
            if (world.ColdArchive.Lookup(id.Value) is { } summary)
                return Result<NpcInspectionDto>.Ok(FromNpcSummary(summary));

            return Result<NpcInspectionDto>.Fail(ensured.Error!);
        }

        return Result<NpcInspectionDto>.Ok(FromLiveNpc(world, world.FindNpc(id)!));
    }

    private static NpcInspectionDto FromLiveNpc(WorldState world, Npc npc)
    {
        long tick = world.CurrentDate.TotalHours;
        return new NpcInspectionDto(
            npc.Id, npc.Name, npc.Sex, npc.AgeYears(world.CurrentDate), npc.Culture, npc.City,
            npc.Household, npc.MotherId, npc.FatherId, npc.Spouse,
            npc.Profession, npc.Employer,
            npc.Health, npc.HungerAt(tick), npc.ThirstAt(tick), npc.SleepAt(tick), npc.SocialAt(tick), npc.Personality, npc.Skills,
            npc.CurrentLocation, npc.CurrentAction, npc.ActionStartedAtTick,
            TargetOf(npc), NpcInspectionLod.Materialized,
            Beliefs: NpcBeliefQuery.BeliefsOf(world, npc.Id),
            Memories: []);
    }

    private static NpcInspectionDto FromNpcSummary(ColdTierArchive.NpcSummary summary)
    {
        var placeholderPersonality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        return new NpcInspectionDto(
            summary.Id, summary.Name, summary.Sex, 0, summary.Culture, default,
            Household: null, MotherId: null, FatherId: null, Spouse: null,
            summary.Profession, Employer: null,
            0, 0, 0, 0, 0, placeholderPersonality, SkillSet.Empty,
            new CellCoord(0, 0), CurrentAction: null, 0,
            ActionTarget: null, Lod: NpcInspectionLod.Archived,
            Beliefs: [],
            Memories: []);
    }

    private static NpcActionTargetDto? TargetOf(Npc npc) => npc.CurrentAction switch
    {
        ActionType.Work when npc.Employer is { } workplace => new("workplace", workplace.Value.ToString()),
        ActionType.Sleep when npc.Household is { } household => new("household", household.Value.ToString()),
        ActionType.Socialize when npc.Spouse is { } spouse => new("npc", spouse.Value.ToString()),
        _ => null,
    };
}
