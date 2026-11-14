using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Venture;

/// <summary>A parte dos sócios no lucro retirado (AD-152): cada um leva a cota dele (<see cref="Workplace.Partners"/>) da retirada
/// do mês; o resto é do dono. Cota de sócio morto passa aos herdeiros dele (<see cref="EstateSystem.HeirsOf"/>, partes iguais, a
/// sobra ao de menor id); sem herdeiro, ou quando quem herda é o próprio dono, a cota volta ao dono. A moeda só muda de dono.</summary>
internal static class VentureShares
{
    /// <returns>Quanto da retirada foi aos sócios.</returns>
    public static long PayPartners(WorldState world, TickContext ctx, Workplace workplace, long draw, string sourceSystem)
    {
        if (workplace.Partners is not { } partners) return 0;
        Settle(world, workplace, partners);
        if (workplace.Partners is not { } current) return 0;

        long paid = 0;
        foreach (var stake in current)
        {
            if (world.FindNpc(stake.Npc) is not { IsAlive: true } partner) continue;
            long part = draw * stake.Basis / WorkplaceStake.Whole;
            if (part <= 0) continue;
            partner.CreditWallet(new Money(part));
            paid += part;
            ctx.LogEvent(WorldEventKind.ProfitDrawn, $"{workplace.Id.Value}|{partner.Id.Value}|{part}", sourceSystem: sourceSystem);
        }

        return paid;
    }

    private static void Settle(WorldState world, Workplace workplace, IReadOnlyList<WorkplaceStake> partners)
    {
        bool changed = false;
        var settled = new List<WorkplaceStake>(partners.Count);
        foreach (var stake in partners)
        {
            if (stake.Npc == workplace.Owner) { changed = true; continue; } // o sócio virou dono: a cota é dele de todo modo
            if (world.FindNpc(stake.Npc) is { IsAlive: true }) { settled.Add(stake); continue; }

            changed = true;
            var heirs = world.FindNpc(stake.Npc) is { } dead ? EstateSystem.HeirsOf(world, dead).Where(h => h.Id != workplace.Owner).ToList() : [];
            if (heirs.Count == 0) continue;
            int share = stake.Basis / heirs.Count;
            for (int i = 0; i < heirs.Count; i++)
                settled.Add(new WorkplaceStake(heirs[i].Id, share + (i == 0 ? stake.Basis - share * heirs.Count : 0)));
        }

        if (changed) workplace.SetPartners(settled);
    }
}
