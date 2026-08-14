using LivingWorld.Domain;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15.1, T2 (VTT2-11): posição de um NPC num tick — mesmo shape de
/// <c>NpcPositionDelta</c> em <c>web/src/data/contracts.ts</c>.</summary>
public sealed record NpcPositionDelta(NpcId NpcId, CellCoord Location);

/// <summary>Fase 15.1, T2 (VTT2-11): só o que mudou entre dois ticks de um escopo — <see
/// cref="Moved"/> cobre NPCs novos ou que trocaram de célula, <see cref="Removed"/> os que
/// saíram do escopo (morreram ou migraram para fora). Mesmo shape de <c>ScopeTickDelta</c> em
/// <c>web/src/data/contracts.ts</c>.</summary>
public sealed record ScopeTickDelta(
    long Tick,
    IReadOnlyList<NpcVisual> NpcUpserts,
    IReadOnlyList<NpcId> NpcRemoved,
    IReadOnlyList<CityVisual> CityUpserts,
    IReadOnlyList<CityId> CityRemoved,
    IReadOnlyList<BuildingVisual> BuildingUpserts,
    IReadOnlyList<BuildingId> BuildingRemoved,
    IReadOnlyList<ProcessVisual> ProcessUpserts,
    IReadOnlyList<long> ProcessRemoved,
    IReadOnlyList<IndicatorUpdate> Indicators,
    IReadOnlyList<NotableVisualEvent> Events)
{
    // Compatibilidade temporária com o consumidor de posição anterior; T4 passa a consumir os
    // upserts tipados diretamente.
    public IReadOnlyList<NpcPositionDelta> Moved => NpcUpserts
        .Select(npc => new NpcPositionDelta(npc.Id, npc.Location)).ToList();
    public IReadOnlyList<NpcId> Removed => NpcRemoved;

    public ScopeTickDelta(long tick, IReadOnlyList<NpcPositionDelta> moved, IReadOnlyList<NpcId> removed)
        : this(tick, moved.Select(item => new NpcVisual(item.NpcId, item.Location, null)).ToList(), removed,
            [], [], [], [], [], [], [], []) { }

    public static ScopeTickDelta Empty(long tick) => new(tick, [], [], [], [], [], [], [], [], [], []);
}
