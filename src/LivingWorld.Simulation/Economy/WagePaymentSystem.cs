using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Paga salário mensal do <see cref="Workplace.Treasury"/> ao <see cref="Npc.Wallet"/>
/// de cada empregado (Fase 5, ECON-21/22), <c>Monthly</c>. Respeita <see
/// cref="EconomyRules.Enabled"/> (ECON-05).</summary>
public sealed class WagePaymentSystem : ISimulationSystem
{
    public const string SystemName = "economy-wage-payment";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var rules = world.EconomyRules;

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            foreach (var employeeId in workplace.Employees.OrderBy(id => id.Value))
            {
                var npc = world.FindNpc(employeeId);
                if (npc is not { IsAlive: true }) continue;
                if (!rules.WageByProfession.TryGetValue(npc.Profession.Id, out var wageAmount)) continue;

                double adjustedAmount = world.FamilyRules.ApplyUpbringingWeight(wageAmount, npc.Upbringing);
                var wage = new Money((long)Math.Round(adjustedAmount));
                var debited = workplace.TryDebitTreasury(wage);
                if (!debited.IsSuccess)
                {
                    ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{workplace.Id.Value}|{wageAmount}");
                    continue; // ECON-22: nem Treasury nem Wallet mudam neste caso
                }

                npc.CreditWallet(wage);
            }
        }
    }
}
