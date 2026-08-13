using LivingWorld.Domain;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15.1, T2 (VTT2-11): posição de um NPC num tick — mesmo shape de
/// <c>NpcPositionDelta</c> em <c>web/src/data/contracts.ts</c>.</summary>
public sealed record NpcPositionDelta(NpcId NpcId, CellCoord Location);

/// <summary>Fase 15.1, T2 (VTT2-11): só o que mudou entre dois ticks de um escopo — <see
/// cref="Moved"/> cobre NPCs novos ou que trocaram de célula, <see cref="Removed"/> os que
/// saíram do escopo (morreram ou migraram para fora). Mesmo shape de <c>ScopeTickDelta</c> em
/// <c>web/src/data/contracts.ts</c>.</summary>
public sealed record ScopeTickDelta(long Tick, IReadOnlyList<NpcPositionDelta> Moved, IReadOnlyList<NpcId> Removed);
