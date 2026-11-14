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

        ReleaseTheDead(world, ctx);
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

        // AD-149: passada a ceifa, a fazenda solta as mãos da ceifa que a terra não paga
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (LivingWorld.Simulation.Economy.Production.CropSystem.HandsPastTheHarvest(world, workplace) is not { } released) continue;
            foreach (var npc in released)
            {
                workplace.Fire(npc.Id);
                npc.Fire();
                ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{workplace.Id.Value}", sourceSystem: "EmploymentSystem");
            }
        }

        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.Employer is not null || !npc.IsAlive) continue;
            if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) continue;
            if (WorkplaceThatHires(world, vacancyIndex, npc) is not { } workplace) continue;
            if (!workplace.Hire(npc.Id).IsSuccess) continue;
            npc.Hire(workplace.Id);
            ctx.LogEvent(WorldEventKind.Hired, $"{npc.Id.Value}|{workplace.Id.Value}", sourceSystem: "EmploymentSystem");
        }
    }

    /// <summary>A oficina da cidade que contrataria <paramref name="npc"/> hoje: a primeira com vaga do ofício dele, que paga a
    /// folha, e que precisa da mão — a encomenda pede (AD-144) e a terra paga o que ela come (AD-149). Nula: não há lugar
    /// para ele. A emigração usa a mesma pergunta (<c>EmigrationSystem</c>).</summary>
    internal static Workplace? WorkplaceThatHires(WorldState world, VacancyIndex vacancyIndex, LivingWorld.Domain.Population.Npc npc, CityId? city = null)
    {
        if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out var locationTypeId)) return null;
        var workplace = vacancyIndex.FirstWorkplaceWithVacancy(locationTypeId, city ?? npc.City);
        if (workplace is null || !Payroll.CanPay(world, workplace, npc.Profession)) return null;
        if (!LivingWorld.Simulation.Economy.Production.ToolMechanic.NeedsHands(world, workplace)) return null;
        if (!LivingWorld.Simulation.Economy.Production.CropSystem.LandNeedsHands(world, workplace)) return null;
        return workplace;
    }

    /// <summary>Quem morreu deixa a vaga (AD-128). O laço acima só percorre os vivos, então o morto ficava na folha da oficina
    /// para sempre: a vaga nunca abria, a geração seguinte crescia sem emprego e sem salário, e o morto nem ia para o
    /// arquivo frio (continuava "referenciado"). Varre as folhas uma vez por dia, em ordem de id.</summary>
    private static void ReleaseTheDead(WorldState world, TickContext ctx)
    {
        List<NpcId>? gone = null;
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            gone?.Clear();
            foreach (var employeeId in workplace.Employees)
                if (world.FindNpc(employeeId) is not { IsAlive: true })
                    (gone ??= []).Add(employeeId);
            if (gone is null || gone.Count == 0) continue;

            gone.Sort((a, b) => a.Value.CompareTo(b.Value));
            foreach (var employeeId in gone)
            {
                workplace.Fire(employeeId);
                world.FindNpc(employeeId)?.Fire();
                ctx.LogEvent(WorldEventKind.Fired, $"{employeeId.Value}|{workplace.Id.Value}", sourceSystem: "EmploymentSystem");
            }
        }
    }
}
