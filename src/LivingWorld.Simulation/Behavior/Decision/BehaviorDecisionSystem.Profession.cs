using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — alocação de trabalho: quem vai para qual oficina.
public sealed partial class BehaviorDecisionSystem
{
    private static ProfessionType? NoneIfSentinel(ProfessionType profession) =>
        profession == ProfessionType.None ? null : profession;

    /// <summary>Quanto o trabalho na melhor oficina precisa valer a mais que o da atual para um empregado trocar.</summary>
    private const double JobSwitchMargin = 1.25;

    /// <summary>Carência entre trocas de emprego, em dias.</summary>
    private const int JobChangeCooldownDays = 30;

    private const int EmployedReviewEveryHours = 6;

    /// <summary>O adulto escolhe a oficina onde o trabalho dele vale mais (valor do trabalho × habilidade na profissão
    /// da oficina), na própria cidade e só com vaga real. Desempregado aceita qualquer ganho; empregado só troca se a
    /// melhor vale <see cref="JobSwitchMargin"/> vezes a atual e a carência passou. A troca é atômica (sai e entra no
    /// mesmo tick, sem demissão sem destino) e o ofício acompanha o emprego. Empate mantém o que já é.</summary>
    private void EvaluateProfessionSwitch(
        WorldState world, TickContext ctx, Npc npc, LaborValueIndex labor, LifeStage stage)
    {
        if (_skillsRules is not { } rules) return;
        if (stage != LifeStage.Adult) return;

        long now = ctx.CurrentTick;
        long cooldown = (long)JobChangeCooldownDays * world.CurrentDate.Calendar.HoursPerDay;
        if (npc.LastJobChangeTick > 0 && now - npc.LastJobChangeTick < cooldown) return;

        // Quem já tem emprego repensa o ofício a cada 6 h (escalonado por id), não a cada despertar.
        if (npc.Employer is not null && (now + npc.Id.Value) % EmployedReviewEveryHours != 0) return;

        var catalog = world.EconomyCatalog;
        if (catalog.LocationTypeByProfession.Count == 0) return;

        var current = npc.Employer is { } employerId ? world.FindWorkplace(employerId) : null;
        // emprego que não paga não vale nada para quem trabalha: é o que faz o trabalhador sair da oficina falida
        double currentValue = current is null || !LivingWorld.Simulation.Economy.Labor.Payroll.CanPay(world, current, npc.Profession)
            ? 0
            : PersonalValue(npc, rules, catalog, labor, current);

        Workplace? best = null;
        double bestValue = 0;
        var candidates = labor.In(npc.City);
        for (int i = 0; i < candidates.Length; i++)
        {
            var workplace = candidates[i];
            if (workplace == current || workplace.Employees.Count >= workplace.MaxVacancies) continue;
            if (ProfessionFor(catalog, labor, workplace.LocationType.Id, npc.Profession) is not { } offered) continue;
            if (!LivingWorld.Simulation.Economy.Labor.Payroll.CanPay(world, workplace, offered)) continue;
            // AD-149: a terra cheia não toma mais uma mão que não paga o que come (a mesma regra da contratação)
            if (!LivingWorld.Simulation.Economy.Production.CropSystem.LandNeedsHands(world, workplace)) continue;

            double value = PersonalValue(npc, rules, catalog, labor, workplace);
            if (value > bestValue)
            {
                best = workplace;
                bestValue = value;
            }
        }

        if (best is null) return;
        if (current is not null && bestValue <= currentValue * JobSwitchMargin) return;

        var profession = ProfessionFor(catalog, labor, best.LocationType.Id, npc.Profession)!.Value;
        if (!world.EconomyRules.Enabled)
        {
            // Sem economia não há emprego a trocar: só o ofício acompanha o que vale mais.
            if (npc.Profession != profession) npc.SwitchProfession(profession);
            return;
        }

        MoveToJob(ctx, npc, current, best, profession, now);
    }

    private static void MoveToJob(
        TickContext ctx, Npc npc, Workplace? from, Workplace to, ProfessionType profession, long now)
    {
        if (from is not null)
        {
            // Quem troca de emprego saiu, não foi demitido: o evento de contratação no destino já conta a troca (AD-128).
            from.Fire(npc.Id);
            npc.Fire();
        }

        to.Hire(npc.Id);
        npc.Hire(to.Id);
        if (npc.Profession != profession) npc.SwitchProfession(profession);
        npc.MarkJobChange(now);
        ctx.LogEvent(WorldEventKind.Hired, $"{npc.Id.Value}|{to.Id.Value}", sourceSystem: "LaborAllocation");
    }

    private static double PersonalValue(
        Npc npc, SkillsRules rules, EconomyCatalog catalog, LaborValueIndex labor, Workplace workplace)
    {
        var profession = ProfessionFor(catalog, labor, workplace.LocationType.Id, npc.Profession);
        double skillWeight = profession is { } p && rules.SkillByProfession.TryGetValue(p.Id, out var skillType)
            ? 1.0 + npc.Skills.Get(skillType) / rules.Cap
            : 1.0;
        // o ofício que combina com a pessoa vale mais para ela (AD-136): quem foi empurrado para o que não combina troca quando pode
        double fit = profession is { } offered ? Population.Skills.VocationMechanic.JobValueFit(rules, npc, offered) : 1.0;
        return labor.ValueOf(workplace, npc) * skillWeight * fit;
    }

    /// <summary>Ofício que trabalha num tipo de local: o atual se já mapeia para ele, senão o de menor id que mapeia.</summary>
    private static ProfessionType? ProfessionFor(
        EconomyCatalog catalog, LaborValueIndex labor, int locationTypeId, ProfessionType current)
    {
        if (catalog.LocationTypeByProfession.TryGetValue(current.Id, out var currentType) && currentType == locationTypeId)
            return current;

        return labor.ProfessionAt(locationTypeId) is { } id ? new ProfessionType(id) : null;
    }
}
