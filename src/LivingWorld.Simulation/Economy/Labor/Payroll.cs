using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
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
}
