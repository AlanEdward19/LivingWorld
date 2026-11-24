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
[RunsAfter(typeof(LivingWorld.Simulation.Economy.Credit.WorkingCapitalSystem))] // AD-155: o dono acha o giro antes da folha
public sealed class WagePaymentSystem : ISimulationSystem
{
    public const string SystemName = "economy-wage-payment";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var rules = world.EconomyRules;

        bool arrears = world.CreditOn;
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (workplace.Closed) { workplace.SetPayrollFailed(true); continue; } // AD-155: fechada por falência
            // Salários devidos neste mês, em ordem de id (determinístico).
            var due = Payroll.Due(world, workplace);
            long totalDue = due.Sum(d => d.Wage.Amount);
            // Folha que não fecha marca o empregador; a marca cai quando o caixa volta a cobrir a folha (ou, sem ninguém na
            // folha, quando há caixa para um salário: é o que deixa a oficina falida recontratar depois de se recuperar).
            long needed = totalDue > 0 ? totalDue : rules.WageByProfession.Count == 0 ? 0 : rules.WageByProfession.Values.Min();
            workplace.SetPayrollFailed(workplace.PayrollFailed ? workplace.Treasury.Amount < needed : totalDue > 0 && workplace.Treasury.Amount < totalDue);
            if (totalDue <= 0) { SettleLeftBoard(world, workplace); continue; }

            // Caixa curto: todos recebem a mesma fração do que lhes é devido (empregador que não fecha a folha
            // atrasa e parcela para todos), em vez de os primeiros por id levarem tudo e os últimos nada.
            double fraction = workplace.Treasury.Amount >= totalDue ? 1.0 : (double)workplace.Treasury.Amount / totalDue;
            foreach (var (npc, wage) in due)
            {
                var pay = fraction >= 1.0 ? wage : new Money((long)Math.Floor(wage.Amount * fraction));
                if (pay.Amount < wage.Amount)
                {
                    ctx.LogEvent(WorldEventKind.WageUnpaid, $"{npc.Id.Value}|{workplace.Id.Value}|{wage.Amount - pay.Amount}", sourceSystem: "WagePaymentSystem");
                    // AD-155: na oficina de dono o salário que faltou fica devido (atraso), pago quando o caixa volta ou na falência
                    if (arrears && workplace.Owner is not null) world.CreditBook.AddArrear(workplace.Id.Value, npc.Id.Value, wage.Amount - pay.Amount);
                }
                if (pay.Amount <= 0) continue;

                // A cidade do empregador retém a alíquota do que o empregador pagou: o líquido vai ao empregado e o resto ao
                // tesouro da cidade, então a moeda total não muda. Oficina sem cidade não retém.
                long tax = rules.WageTaxRate > 0 && world.FindCity(workplace.City) is { } city
                    ? (long)Math.Floor(pay.Amount * rules.WageTaxRate)
                    : 0;
                // AD-151: as refeições da caderneta saem do líquido (até BoardMaxWageShare dele) e ficam no caixa, onde a comida já
                // saiu do estoque; o que passa disso a oficina paga em espécie (a refeição é parte do salário, não dívida)
                long owed = workplace.BoardOwedBy(npc.Id);
                long board = Math.Min(owed, (long)Math.Floor((pay.Amount - tax) * Math.Clamp(rules.BoardMaxWageShare, 0, 1)));
                var debited = workplace.TryDebitTreasury(new Money(pay.Amount - board));
                if (!debited.IsSuccess) continue;
                if (owed > 0)
                {
                    workplace.AddBoard(npc.Id, -owed);
                    world.PurchaseTally.BoardWithheld += board;
                    world.PurchaseTally.BoardInKind += owed - board;
                }
                if (tax > 0)
                {
                    world.FindCity(workplace.City)!.CreditTreasury(new Money(tax));
                    world.RecordTreasury(workplace.City, ctx.CurrentTick, revenue: tax, spending: 0);
                }

                npc.CreditWallet(new Money(pay.Amount - tax - board));
            }
            SettleLeftBoard(world, workplace);
        }
    }

    /// <summary>AD-151: quem deixou a caderneta e não está mais na folha (saiu, foi demitido, morreu) paga da carteira o que puder;
    /// o resto o empregador perde (contado em <c>PurchaseTally.BoardForgiven</c>). A moeda vai da carteira ao caixa; nada se cria.</summary>
    private static void SettleLeftBoard(WorldState world, Workplace workplace)
    {
        if (workplace.Board is not { } board) return;
        List<(BoardDebt Debt, Npc? Npc)>? gone = null;
        foreach (var debt in board)
        {
            var npc = world.FindNpc(debt.Npc);
            if (npc is { IsAlive: true } && npc.Employer == workplace.Id) continue; // ainda aqui: o próximo salário desconta
            (gone ??= []).Add((debt, npc is { IsAlive: true } ? npc : null));
        }
        if (gone is null) return;
        foreach (var (debt, npc) in gone)
        {
            long paid = npc is null ? 0 : Math.Min(debt.Owed, npc.Wallet.Amount);
            if (paid > 0 && npc!.TryDebitWallet(new Money(paid)).IsSuccess) workplace.CreditTreasury(new Money(paid));
            else paid = 0;
            workplace.AddBoard(debt.Npc, -debt.Owed);
            world.PurchaseTally.BoardCollected += paid;
            world.PurchaseTally.BoardForgiven += debt.Owed - paid;
        }
    }
}
