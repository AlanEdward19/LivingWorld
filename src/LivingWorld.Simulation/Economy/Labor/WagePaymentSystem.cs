using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Paga salário mensal do <see cref="Workplace.Treasury"/> ao <see cref="Npc.Wallet"/>
/// de cada empregado, <c>Monthly</c>. Respeita <see
/// cref="EconomyRules.Enabled"/>.</summary>
[LivingCapability("WAGES", Events = [WorldEventKind.WageUnpaid], Consumers = ["timeline.wages"])]
[RunsAfter(typeof(MarketPricingSystem))]
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
            // Salários devidos neste mês, em ordem de id (determinístico).
            var due = new List<(Npc Npc, Money Wage)>();
            foreach (var employeeId in workplace.Employees.OrderBy(id => id.Value))
            {
                var npc = world.FindNpc(employeeId);
                if (npc is not { IsAlive: true }) continue;
                if (!rules.WageByProfession.TryGetValue(npc.Profession.Id, out var wageAmount)) continue;

                double adjustedAmount = world.FamilyRules.ApplyUpbringingWeight(wageAmount, npc.Upbringing);
                due.Add((npc, new Money((long)Math.Round(adjustedAmount))));
            }

            long totalDue = due.Sum(d => d.Wage.Amount);
            if (totalDue <= 0) continue;

            // Caixa curto: todos recebem a mesma fração do que lhes é devido (empregador que não fecha a folha
            // atrasa e parcela para todos), em vez de os primeiros por id levarem tudo e os últimos nada.
            double fraction = workplace.Treasury.Amount >= totalDue ? 1.0 : (double)workplace.Treasury.Amount / totalDue;
            foreach (var (npc, wage) in due)
            {
                var pay = fraction >= 1.0 ? wage : new Money((long)Math.Floor(wage.Amount * fraction));
                if (pay.Amount < wage.Amount)
                    ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{workplace.Id.Value}|{wage.Amount - pay.Amount}", sourceSystem: "WagePaymentSystem");
                if (pay.Amount <= 0) continue;

                var debited = workplace.TryDebitTreasury(pay);
                if (!debited.IsSuccess) continue;

                // A cidade do empregador retém a alíquota do que o empregador pagou: o líquido vai ao empregado e o resto ao
                // tesouro da cidade, então a moeda total não muda. Oficina sem cidade não retém.
                long tax = rules.WageTaxRate > 0 && world.FindCity(workplace.City) is { } city
                    ? (long)Math.Floor(pay.Amount * rules.WageTaxRate)
                    : 0;
                if (tax > 0)
                {
                    world.FindCity(workplace.City)!.CreditTreasury(new Money(tax));
                    world.RecordTreasury(workplace.City, ctx.CurrentTick, revenue: tax, spending: 0);
                }

                npc.CreditWallet(new Money(pay.Amount - tax));
            }
        }
    }
}
