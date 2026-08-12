namespace LivingWorld.Domain;

/// <summary>Escopo de interior de um NPC (Fase 15.1, T47/ADR-0017): prédio, andar e célula
/// local. Sempre tudo-ou-nada (um único campo nulável em <see cref="Npc.Interior"/>, nunca 3
/// campos soltos) — exclusividade de escopo por construção: um NPC não pode estar "meio dentro"
/// de um prédio.</summary>
public sealed record InteriorOccupancy(BuildingId Building, FloorLevel Floor, CellCoord LocalCell);
