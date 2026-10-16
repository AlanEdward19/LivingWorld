using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Liga NPC adulto desempregado a um <see cref="Workplace"/> com vaga livre,
/// <c>Daily</c> (vaga não precisa reagir por hora). Respeita
/// <see cref="EconomyRules.Enabled"/> (desligar a economia desliga este sistema
/// também).</summary>
[LivingCapability("EMPLOYMENT", Events = [WorldEventKind.Hired, WorldEventKind.Fired], Consumers = ["inspector.employment"])]
[RunsAfter(typeof(BehaviorDecisionSystem))]
public sealed class EmploymentSystem : ISimulationSystem
{
    public const string SystemName = "economy-employment";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var vacancyIndex = VacancyIndex.BuildForTick(world);

        // Desliga primeiro quem ficou órfão (Workplace sumiu ou o próprio NPC morreu) — nunca um
        // Npc.Employer aponta pra Workplace inexistente ao fim do tick.
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.Employer is not { } employerId) continue;
            var workplace = world.FindWorkplace(employerId);
            if (workplace is not null && npc.IsAlive) continue;

            workplace?.Fire(npc.Id);
            npc.Fire();
            ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{employerId.Value}", sourceSystem: "EmploymentSystem");
        }

        var catalog = world.EconomyCatalog;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.Employer is not null) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            if (!catalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out var locationTypeId)) continue;

            var workplace = vacancyIndex.FirstWorkplaceWithVacancy(locationTypeId, npc.City);
            if (workplace is null) continue;

            if (!workplace.Hire(npc.Id).IsSuccess) continue;
            npc.Hire(workplace.Id);
            ctx.LogEvent(WorldEventKind.Hired, $"{npc.Id.Value}|{workplace.Id.Value}", sourceSystem: "EmploymentSystem");
        }
    }
}
