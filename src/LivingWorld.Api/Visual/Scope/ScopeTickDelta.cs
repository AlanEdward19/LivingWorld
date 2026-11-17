using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Posição de um NPC num tick — mesmo shape de
/// <c>NpcPositionDelta</c> em <c>web/src/data/contracts.ts</c>.</summary>
public sealed record NpcPositionDelta(NpcId NpcId, CellCoord Location);

/// <summary>Só o que mudou entre dois ticks de um escopo — <see
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
    // Compatibilidade temporária com o consumidor de posição anterior, até ele passar a
    // consumir os upserts tipados diretamente.
    public IReadOnlyList<NpcPositionDelta> Moved => NpcUpserts
        .Select(npc => new NpcPositionDelta(npc.Id, npc.Location)).ToList();
    public IReadOnlyList<NpcId> Removed => NpcRemoved;

    public ScopeTickDelta(long tick, IReadOnlyList<NpcPositionDelta> moved, IReadOnlyList<NpcId> removed)
        : this(tick, moved.Select(item => new NpcVisual(item.NpcId, item.Location, null)).ToList(), removed,
            [], [], [], [], [], [], [], [])
    { }

    // AD-170 (ADR-0047 etapa 2): o observável do escopo, só o que mudou. Clima vem nulo quando não mudou.
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.BuildingStateVisual> BuildingStateUpserts { get; init; } = [];
    public IReadOnlyList<long> BuildingStateRemoved { get; init; } = [];
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.CropVisual> CropUpserts { get; init; } = [];
    public IReadOnlyList<long> CropRemoved { get; init; } = [];
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.AnimalVisual> AnimalUpserts { get; init; } = [];
    public IReadOnlyList<long> AnimalRemoved { get; init; } = [];
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.FaunaGroupVisual> FaunaGroupUpserts { get; init; } = [];
    public IReadOnlyList<string> FaunaGroupRemoved { get; init; } = [];
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.LeavingVisual> LeavingUpserts { get; init; } = [];
    public IReadOnlyList<long> LeavingRemoved { get; init; } = [];
    public IReadOnlyList<LivingWorld.Api.Visual.Observable.ObjectVisual> ObjectUpserts { get; init; } = [];
    public IReadOnlyList<long> ObjectRemoved { get; init; } = [];
    public LivingWorld.Api.Visual.Observable.ScopeClimateVisual? Climate { get; init; }
    // AD-176: rua de verdade por célula, só o que mudou (superfície, faixa de uso, obra, condição)
    public IReadOnlyList<LivingWorld.Simulation.Cities.StreetCellView> RoadUpserts { get; init; } = [];
    public IReadOnlyList<CellCoord> RoadRemoved { get; init; } = [];

    public static ScopeTickDelta Empty(long tick) => new(tick, [], [], [], [], [], [], [], [], [], []);
}
