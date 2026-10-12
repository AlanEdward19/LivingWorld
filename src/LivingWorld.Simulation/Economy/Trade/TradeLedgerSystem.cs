using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Trade;

/// <summary>Primeiro contato de compra: quem comprou e quem estava atendendo a oficina. Fila volátil, esvaziada a cada
/// hora; só o primeiro contato de cada par chega aqui, então o log de eventos cresce com os pares, não com as compras.</summary>
internal readonly record struct TradeContact(NpcId Buyer, NpcId Attendant, WorkplaceId Workplace, int ResourceId, long Value);

/// <summary>Livro de trocas, <c>Hourly</c>: grava o evento <see cref="WorldEventKind.Trade"/> dos primeiros contatos
/// comprador–atendente e, na virada do dia, compacta em mês os dias que saíram da janela de detalhe. A soma de cada compra
/// ao agregado do dia e a relação do comprador com quem atende acontecem na própria compra (<c>TryApplyBuy</c>).</summary>
[LivingCapability("TRADE_LEDGER", Events = [WorldEventKind.Trade], Consumers = ["inspector.workplace.trades"])]
[RunsAfter(typeof(BehaviorDecisionSystem))]
public sealed class TradeLedgerSystem : ISimulationSystem
{
    public const string SystemName = "economy-trade-ledger";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var pending = world.PendingTradeContacts;
        if (pending.Count > 0)
        {
            foreach (var contact in pending)
                ctx.LogEvent(
                    WorldEventKind.Trade,
                    $"{contact.Buyer.Value}|{contact.Attendant.Value}|{contact.Workplace.Value}|{contact.ResourceId}|{contact.Value}",
                    sourceSystem: SystemName);
            pending.Clear();
        }

        int hoursPerDay = world.CurrentDate.Calendar.HoursPerDay;
        if (ctx.CurrentTick % hoursPerDay == 0)
            world.CompactTradeLedger(ctx.CurrentTick / hoursPerDay);
    }

    /// <summary>Quem atende a oficina na hora: o empregado vivo de menor id que está no local dela. Sem ninguém, a
    /// compra só entra no livro (nada de relação). Determinístico e sem alocação.</summary>
    internal static Npc? AttendantAt(WorldState world, Workplace workplace)
    {
        Npc? best = null;
        var employees = workplace.Employees;
        for (int i = 0; i < employees.Count; i++)
        {
            if (world.FindNpc(employees[i]) is not { IsAlive: true } npc) continue;
            if (npc.CurrentLocation != workplace.Location) continue;
            if (best is null || npc.Id.Value < best.Id.Value) best = npc;
        }

        return best;
    }
}
