using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Retirada do lucro (AD-144), <c>Monthly</c>, logo depois da folha: o caixa acima da reserva de giro
/// (<see cref="Payroll.WorkingReserve"/>) sai na fração <see cref="EconomyRules.SurplusDrawShare"/>. Com dono vivo vai para a
/// carteira dele (e dali para a casa, a ajuda aos parentes e a herança); sem dono (terra comum, obra da cidade, espólio sem
/// herdeiro) vai ao tesouro da cidade, que paga a esmola. Absorve o dízimo provisório da AD-140: é o mesmo fluxo, agora com
/// destino certo. A moeda só muda de dono; a reserva fica, então a retirada nunca deixa a folha seguinte sem caixa.</summary>
[LivingCapability("WAGES", Events = [WorldEventKind.ProfitDrawn], Consumers = ["timeline.wages"])]
[RunsAfter(typeof(WagePaymentSystem))]
public sealed class OwnerDrawSystem : ISimulationSystem
{
    public const string SystemName = "economy-owner-draw";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.EconomyRules;
        if (!rules.Enabled || rules.SurplusDrawShare <= 0) return;

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            long excess = workplace.Treasury.Amount - Payroll.WorkingReserve(world, workplace);
            long draw = (long)Math.Floor(excess * Math.Min(1.0, rules.SurplusDrawShare));
            if (draw <= 0) continue;

            if (workplace.Owner is { } ownerId && world.FindNpc(ownerId) is { IsAlive: true } owner)
            {
                if (!workplace.TryDebitTreasury(new Money(draw)).IsSuccess) continue;
                owner.CreditWallet(new Money(draw));
                ctx.LogEvent(WorldEventKind.ProfitDrawn, $"{workplace.Id.Value}|{owner.Id.Value}|{draw}", sourceSystem: SystemName);
                continue;
            }

            if (world.FindCity(workplace.City) is not { } city || !workplace.TryDebitTreasury(new Money(draw)).IsSuccess) continue;
            city.CreditTreasury(new Money(draw));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: draw, spending: 0);
            ctx.LogEvent(WorldEventKind.ProfitDrawn, $"{workplace.Id.Value}|city-{city.Id.Value}|{draw}", sourceSystem: SystemName);
        }
    }
}
