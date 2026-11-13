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

    /// <summary>Reserva de giro (AD-140/AD-144): <see cref="EconomyRules.WorkingCapitalPayrollMonths"/> meses da folha atual,
    /// nunca menos que um salário do ofício mais barato (a oficina vazia guarda o bastante para recontratar).</summary>
    public static long WorkingReserve(WorldState world, Workplace workplace)
    {
        var rules = world.EconomyRules;
        long payroll = 0;
        foreach (var (_, wage) in Due(world, workplace)) payroll += wage.Amount;
        long minWage = rules.WageByProfession.Count == 0 ? 0 : rules.WageByProfession.Values.Min();
        return (long)Math.Ceiling(Math.Max(payroll, minWage) * Math.Max(0, rules.WorkingCapitalPayrollMonths));
    }
}
