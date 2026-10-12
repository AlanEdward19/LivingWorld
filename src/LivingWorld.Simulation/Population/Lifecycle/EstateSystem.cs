using LivingWorld.Domain.Economy;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Lifecycle;

/// <summary>Espólio (estate-inheritance): na morte, a carteira do morto passa aos herdeiros em ordem fixa (cônjuge vivo,
/// depois filhos, depois pais, depois irmãos; sem ninguém, ao tesouro da cidade), em partes iguais com o resto ao de menor
/// id. A despensa do domicílio que fica sem ninguém vai ao domicílio do primeiro herdeiro (em vez de se perder) e a oficina
/// de que ele era dono passa ao herdeiro de menor id presente nela (ou, se nenhum está lá, ao de menor id). Nada é criado
/// nem destruído: o saldo do mundo é o mesmo antes e depois da morte. Chamado por <see cref="NpcDeath"/>, uma vez por morte.</summary>
public static class EstateSystem
{
    public const string SystemName = "EstateSystem";

    public static void Settle(WorldState world, TickContext ctx, Npc dead)
    {
        long wallet = dead.Wallet.Amount;
        var owned = OwnedWorkplaces(world, dead);
        var household = dead.Household is { } householdId ? world.FindHousehold(householdId) : null;
        bool lastInHousehold = household is { } h && h.Members.Count == 1 && h.Members[0] == dead.Id;
        long stockUnits = lastInHousehold ? household!.Stock.Values.Sum() : 0;
        if (wallet <= 0 && stockUnits <= 0 && owned.Count == 0) return;

        var heirs = HeirsOf(world, dead);
        long toCity = 0;
        if (wallet > 0)
        {
            if (heirs.Count > 0) PayHeirs(dead, heirs, wallet);
            else toCity = PayCity(world, dead, wallet);
        }

        long movedStock = stockUnits > 0 && heirs.Count > 0 ? MoveStock(world, household!, heirs) : 0;
        int transferred = TransferWorkplaces(world, owned, heirs);
        foreach (var heir in heirs)
        {
            var relationship = world.GetOrCreateRelationship(new RelationshipKey(dead.Id, heir.Id), world.CurrentDate.TotalHours);
            relationship.ApplyEvent(RelationshipEventType.Inheritance, world.FamilyRules);
            relationship.MarkContact(world.CurrentDate.TotalHours);
        }

        ctx.LogEvent(
            WorldEventKind.EstateSettled,
            $"{dead.Id.Value}|{string.Join(',', heirs.Select(x => x.Id.Value))}|{(heirs.Count > 0 ? wallet : toCity)}|{movedStock}|{transferred}",
            sourceSystem: SystemName);
    }

    /// <summary>Herdeiros vivos, no primeiro grau que tem alguém, por id crescente (determinístico).</summary>
    internal static List<Npc> HeirsOf(WorldState world, Npc dead)
    {
        if (dead.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true } spouse)
            return [spouse];

        var children = world.Npcs.Where(n => n.IsAlive && (n.MotherId == dead.Id || n.FatherId == dead.Id)).OrderBy(n => n.Id.Value).ToList();
        if (children.Count > 0) return children;

        var parents = new List<Npc>();
        foreach (var parentId in new[] { dead.MotherId, dead.FatherId })
            if (parentId is { } id && world.FindNpc(id) is { IsAlive: true } parent) parents.Add(parent);
        if (parents.Count > 0) return parents.OrderBy(n => n.Id.Value).ToList();

        return world.Npcs
            .Where(n => n.IsAlive && n.Id != dead.Id
                && ((dead.MotherId is { } m && n.MotherId == m) || (dead.FatherId is { } f && n.FatherId == f)))
            .OrderBy(n => n.Id.Value).ToList();
    }

    private static void PayHeirs(Npc dead, List<Npc> heirs, long wallet)
    {
        _ = dead.TryDebitWallet(new Money(wallet));
        long share = wallet / heirs.Count;
        long remainder = wallet - share * heirs.Count;
        for (int i = 0; i < heirs.Count; i++)
            heirs[i].CreditWallet(new Money(share + (i == 0 ? remainder : 0)));
    }

    private static long PayCity(WorldState world, Npc dead, long wallet)
    {
        if (world.FindCity(dead.City) is not { } city) return 0; // sem cidade: o dinheiro fica com o morto, como antes

        _ = dead.TryDebitWallet(new Money(wallet));
        city.CreditTreasury(new Money(wallet));
        world.RecordTreasury(city.Id, world.CurrentDate.TotalHours, revenue: wallet, spending: 0);
        return wallet;
    }

    private static long MoveStock(WorldState world, Household from, List<Npc> heirs)
    {
        var target = heirs.Select(h => h.Household is { } id ? world.FindHousehold(id) : null).FirstOrDefault(x => x is not null && x.Id != from.Id);
        if (target is null) return 0;

        long moved = 0;
        foreach (var (resource, amount) in from.Stock.ToList())
        {
            if (amount <= 0 || !from.Withdraw(resource, amount).IsSuccess) continue;
            target.Deposit(resource, amount);
            moved += amount;
        }

        return moved;
    }

    private static List<Workplace> OwnedWorkplaces(WorldState world, Npc dead) =>
        world.Workplaces.Where(w => w.Owner == dead.Id).ToList();

    private static int TransferWorkplaces(WorldState world, List<Workplace> owned, List<Npc> heirs)
    {
        foreach (var workplace in owned)
        {
            var heir = heirs.FirstOrDefault(h => h.CurrentLocation == workplace.Location) ?? heirs.FirstOrDefault();
            workplace.AssignOwner(heir?.Id);
        }

        return heirs.Count > 0 ? owned.Count : 0;
    }
}
