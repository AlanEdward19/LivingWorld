using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Liga NPC adulto desempregado a um <see cref="Workplace"/> com vaga livre (Fase 5,
/// ECON-18/19/20), <c>Daily</c> (AD-042 — vaga não precisa reagir por hora). Respeita
/// <see cref="EconomyRules.Enabled"/> (ECON-05: desligar a economia desliga este sistema
/// também).</summary>
public sealed class EmploymentSystem : ISimulationSystem
{
    public const string SystemName = "economy-employment";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        // Desliga primeiro quem ficou órfão (Workplace sumiu ou o próprio NPC morreu) — nunca um
        // Npc.Employer aponta pra Workplace inexistente ao fim do tick (ECON-20/ECON-04).
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (npc.Employer is not { } employerId) continue;
            var workplace = world.FindWorkplace(employerId);
            if (workplace is not null && npc.IsAlive) continue;

            workplace?.Fire(npc.Id);
            npc.Fire();
            ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{employerId.Value}");
        }

        var catalog = world.EconomyCatalog;
        foreach (var npc in world.Npcs.OrderBy(n => n.Id.Value))
        {
            if (!npc.IsAlive || npc.Employer is not null) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            if (!catalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out var locationTypeId)) continue;

            var workplace = world.Workplaces
                .Where(w => w.LocationType.Id == locationTypeId && w.Employees.Count < w.MaxVacancies)
                .OrderBy(w => w.Id.Value)
                .FirstOrDefault();
            if (workplace is null) continue;

            if (!workplace.Hire(npc.Id).IsSuccess) continue;
            npc.Hire(workplace.Id);
            ctx.LogEvent(WorldEventKind.Hired, $"{npc.Id.Value}|{workplace.Id.Value}");
        }
    }
}
