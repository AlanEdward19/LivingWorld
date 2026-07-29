using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION: design.md pede este tipo em src/LivingWorld.Domain/Cities/ — mesmo
// SPEC_DEVIATION já registrado em CityPopulationQuery.cs: LivingWorld.Domain não referencia
// LivingWorld.Simulation, e WorldState só existe em Simulation. Vive aqui.

/// <summary>Único ponto de consulta de inspeção de NPC (Fase 8, T14, CITY-06) — API e CLI
/// chamam este método, nenhuma lógica duplicada entre os dois (AC #2 da story P1). Materializa
/// sob demanda (<see cref="MaterializationSystem.EnsureMaterialized"/>) antes de montar o DTO;
/// falha para id inexistente ou morto (AC #3).</summary>
public static class NpcInspectionQuery
{
    public static Result<NpcInspectionDto> Inspect(WorldState world, NpcId id)
    {
        var ensured = MaterializationSystem.EnsureMaterialized(world, id);
        if (!ensured.IsSuccess)
        {
            if (world.ColdArchive.Lookup(id.Value) is { } summary)
            {
                var placeholderPersonality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
                return Result<NpcInspectionDto>.Ok(new NpcInspectionDto(
                    summary.Id, summary.Name, summary.Sex, 0, summary.Culture, default,
                    Household: null, MotherId: null, FatherId: null, Spouse: null,
                    summary.Profession, Employer: null,
                    0, 0, 0, 0, 0, placeholderPersonality, SkillSet.Initial(0),
                    new CellCoord(0, 0), CurrentAction: null, 0,
                    Memories: []));
            }

            return Result<NpcInspectionDto>.Fail(ensured.Error!);
        }

        var npc = world.FindNpc(id)!;

        long tick = world.CurrentDate.TotalHours;
        var dto = new NpcInspectionDto(
            npc.Id, npc.Name, npc.Sex, npc.AgeYears(world.CurrentDate), npc.Culture, npc.City,
            npc.Household, npc.MotherId, npc.FatherId, npc.Spouse,
            npc.Profession, npc.Employer,
            npc.Health, npc.HungerAt(tick), npc.ThirstAt(tick), npc.SleepAt(tick), npc.SocialAt(tick), npc.Personality, npc.Skills,
            npc.CurrentLocation, npc.CurrentAction, npc.ActionStartedAtTick,
            Memories: []);

        return Result<NpcInspectionDto>.Ok(dto);
    }
}
