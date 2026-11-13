using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Se o empregador vem pagando o que deve (AD-128): falso depois de uma folha que não fechou, até voltar a ter caixa. Ninguém escolhe, nem fica anos, num
/// emprego que não paga: sem isso a oficina falida segurava dezenas de trabalhadores sem salário enquanto havia vaga paga
/// ao lado. Economia desligada ou ofício sem salário declarado: sempre pode.</summary>
internal static class Payroll
{
    public static bool CanPay(WorldState world, Workplace workplace, ProfessionType profession)
    {
        var rules = world.EconomyRules;
        if (!rules.Enabled || !rules.WageByProfession.ContainsKey(profession.Id)) return true;
        return !workplace.PayrollFailed;
    }

    /// <summary>Salário do mês de cada empregado vivo com ofício assalariado, em ordem de id (o peso da criação já aplicado).</summary>
    public static List<(Npc Npc, Money Wage)> Due(WorldState world, Workplace workplace)
    {
        var rules = world.EconomyRules;
        var due = new List<(Npc Npc, Money Wage)>();
        foreach (var employeeId in workplace.Employees.OrderBy(id => id.Value))
        {
            var npc = world.FindNpc(employeeId);
            if (npc is not { IsAlive: true }) continue;
            if (!rules.WageByProfession.TryGetValue(npc.Profession.Id, out var wageAmount)) continue;

            double adjustedAmount = world.FamilyRules.ApplyUpbringingWeight(wageAmount, npc.Upbringing);
            due.Add((npc, new Money((long)Math.Round(adjustedAmount))));
        }

        return due;
    }

    /// <summary>Reserva de giro (AD-140/AD-144): <see cref="EconomyRules.WorkingCapitalPayrollMonths"/> meses da folha que o
    /// local pode ter de pagar: a maior entre a folha atual e a lotação possível (os adultos da cidade, até as vagas, ao salário
    /// do ofício do local), nunca menos que um salário. Só a folha do dia deixava a oficina que estava vazia no fim do mês com
    /// um salário de reserva: a gente voltava, a folha não fechava e todos fugiam dela (achado na vila de 30, AD-144).</summary>
    public static long WorkingReserve(WorldState world, Workplace workplace)
    {
        var rules = world.EconomyRules;
        long payroll = 0;
        foreach (var (_, wage) in Due(world, workplace)) payroll += wage.Amount;

        long tradeWage = 0;
        foreach (var (profession, type) in world.EconomyCatalog.LocationTypeByProfession)
            if (type == workplace.LocationType.Id && rules.WageByProfession.TryGetValue(profession, out var wage))
                tradeWage = Math.Max(tradeWage, wage);

        long staff = 0;
        if (tradeWage > 0)
            foreach (var npc in world.AliveNpcIndex.Alive)
            {
                if (staff >= workplace.MaxVacancies) break;
                if (npc.City == workplace.City && npc.AgeYears(world.CurrentDate) >= 15) staff++;
            }

        long minWage = rules.WageByProfession.Count == 0 ? 0 : rules.WageByProfession.Values.Min();
        return (long)Math.Ceiling(Math.Max(Math.Max(payroll, staff * tradeWage), minWage) * Math.Max(0, rules.WorkingCapitalPayrollMonths));
    }
}
