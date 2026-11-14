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
/// destino certo. A oficina sem dono é da cidade também na falta: abaixo da reserva, o tesouro a recompõe (a fazenda comum dá
/// prejuízo no salário fixo e, sem isso, quebrava em ~6 anos). A moeda só muda de dono; a reserva protege a folha seguinte. Os sócios
/// de quem abriu a oficina com eles levam a cota deles antes do dono (AD-152).</summary>
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
            if (excess < 0 && workplace.Owner is null)
            {
                // a oficina sem dono é da cidade: o senhorio que leva o lucro também cobre a falta, até a reserva (AD-144)
                if (world.FindCity(workplace.City) is { } lord && lord.Treasury.Amount > 0)
                {
                    long cover = Math.Min(-excess, lord.Treasury.Amount);
                    if (lord.TryDebitTreasury(new Money(cover)).IsSuccess)
                    {
                        workplace.CreditTreasury(new Money(cover));
                        world.RecordTreasury(lord.Id, ctx.CurrentTick, revenue: 0, spending: cover);
                    }
                }

                continue;
            }

            long draw = (long)Math.Floor(excess * Math.Min(1.0, rules.SurplusDrawShare));
            if (draw <= 0) continue;

            if (workplace.Owner is { } ownerId && world.FindNpc(ownerId) is { IsAlive: true } owner)
            {
                if (!workplace.TryDebitTreasury(new Money(draw)).IsSuccess) continue;
                long rest = draw - Venture.VentureShares.PayPartners(world, ctx, workplace, draw, SystemName); // AD-152: cota dos sócios
                owner.CreditWallet(new Money(rest));
                ctx.LogEvent(WorldEventKind.ProfitDrawn, $"{workplace.Id.Value}|{owner.Id.Value}|{rest}", sourceSystem: SystemName);
                continue;
            }

            if (world.FindCity(workplace.City) is not { } city || !workplace.TryDebitTreasury(new Money(draw)).IsSuccess) continue;
            long toCity = draw - Venture.VentureShares.PayPartners(world, ctx, workplace, draw, SystemName);
            city.CreditTreasury(new Money(toCity));
            world.RecordTreasury(city.Id, ctx.CurrentTick, revenue: toCity, spending: 0);
            ctx.LogEvent(WorldEventKind.ProfitDrawn, $"{workplace.Id.Value}|city-{city.Id.Value}|{toCity}", sourceSystem: SystemName);
        }
    }
}
