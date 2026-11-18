using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — destino da viagem por motivo (trabalho, sono, compra, mudança).
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>Onde o NPC trabalha: a oficina do emprego; sem emprego, o trecho da obra em que é diarista (AD-179).</summary>
    private static CellCoord? WorkDestinationOf(WorldState world, Npc npc) =>
        npc.Employer is { } workplaceId ? world.FindWorkplace(workplaceId)?.Location
            : LivingWorld.Simulation.Cities.Streets.SiteWork.DestinationOf(world, npc);

    private static CellCoord? SleepDestinationOf(WorldState world, Npc npc)
    {
        var rest = RestPlaceResolver.Resolve(world, npc);
        if (rest.Location == npc.CurrentLocation) return null;
        return RestPlaceResolver.IsReachable(world.Map, npc.CurrentLocation, rest.Location) ? rest.Location : null;
    }

    private static CellCoord? BuyDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        NearestEssentialsMarket(world, npc, marketIndex)?.Location;

    /// <summary>Mercado mais próximo com comida ou água em estoque — quem vai comprar não vai à
    /// ferraria só porque ela fica mais perto.</summary>
    private static Workplace? NearestEssentialsMarket(WorldState world, Npc npc, MarketIndex marketIndex) =>
        marketIndex.NearestSelling(
            npc.CurrentLocation,
            new ResourceType(world.EconomyRules.FoodResourceId), new ResourceType(world.EconomyRules.WaterResourceId));

    /// <summary>AD-176: para onde a viagem em curso vai (a projeção desenha a rota até lá).</summary>
    internal static CellCoord? TravelDestinationFor(WorldState world, Npc npc, MarketIndex marketIndex) => TravelDestinationOf(world, npc, marketIndex);

    private static CellCoord? TravelDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex) =>
        RelocationDestinationOf(world, npc) is { } relocationDest
            ? relocationDest
            // AD-186: quem saiu atrás de companhia e a achou no caminho (ou perdeu o rumo) fica onde está, não segue à cama nem à banca
            : npc.CurrentAction == ActionType.Travel && npc.TravelPurpose == ActionType.Socialize && !OffShiftFarFromPantry(world, npc)
                ? CompanyDestinationOf(world, npc) ?? npc.CurrentLocation
            : PurposeDestinationOf(world, npc, marketIndex) is { } purposeDest
                ? purposeDest
                : LivingWorld.Simulation.Invention.Leavings.ErrandDestination(world, npc) is { } errand && errand != npc.CurrentLocation
                    ? errand // AD-168: a ida para buscar o espólio de alguém da casa
                : SleepDestinationOf(world, npc) is { } sleepDest && sleepDest != npc.CurrentLocation
                    ? sleepDest
                    : WorkDestinationOf(world, npc) is { } workDest && workDest != npc.CurrentLocation
                        ? workDest
                        : BuyDestinationOf(world, npc, marketIndex);

    /// <summary>Destino do motivo da viagem (ir comprar, trabalhar ou dormir); nulo se o motivo é outro ou o
    /// NPC já está lá — e então vale a ordem de antes.</summary>
    private static CellCoord? PurposeDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        var destination = npc.TravelPurpose switch
        {
            ActionType.Buy => BuyDestinationOf(world, npc, marketIndex),
            ActionType.Work => WorkDestinationOf(world, npc),
            ActionType.Sleep => SleepDestinationOf(world, npc),
            ActionType.Eat => EatDestinationOf(world, npc),
            _ => null,
        };
        return destination is { } cell && cell != npc.CurrentLocation ? cell : null;
    }

    /// <summary>AD-186: para onde vai quem quer conversar e não tem ninguém perto (<see cref="PsycheRules.SeekCompany"/>): a casa do
    /// conhecido vivo da mesma cidade de quem mais gosta (afeto acima de zero; empate pelo menor id), se ele está em casa, se lá fica a até
    /// <c>SeekCompanyMaxCells</c> células da casa; sem ninguém, o largo da cidade (o centro), se há gente lá. Nulo: há alguém por perto, a regra está
    /// desligada, ou não há aonde ir. Só lê; a vizinhança é a mesma janela que a conversa olha (<c>SocializeRadius</c>).</summary>
    internal static CellCoord? CompanyDestinationOf(WorldState world, Npc npc)
    {
        var psyche = world.NeedsRules.Psyche;
        if (!psyche.Enabled || !psyche.SocializeNeedsCompany || !psyche.SeekCompany || world.FamilyRules.Bonds is not { } bonds) return null;
        if (npc.Household is not { } hh || world.FindHousehold(hh) is not { } home) return null;
        // a ida em curso vai ao destino escolhido na saída, guardado no intento (o motivo da viagem fica no NPC depois de chegar:
        // só vale enquanto ainda viaja); não se escolhe de novo a cada despertar
        if (npc.CurrentAction == ActionType.Travel && npc.TravelPurpose == ActionType.Socialize) return CompanyTargetOf(npc);
        // só quem está em casa e sente falta de gente (convívio abaixo da linha de urgência) sai à procura; o resto conversa onde está
        long now = world.CurrentDate.TotalHours;
        if (WhyNotSeeking(world, npc, home, now) != SeekGate.Open) return null;
        // a mesma pergunta volta no refino, na saída, na chegada agendada e na conclusão do mesmo tick: só memória do tick
        var last = _company;
        if (ReferenceEquals(last.World, world) && last.Npc == npc.Id.Value && last.Tick == now && last.From == npc.CurrentLocation)
            return last.Result;
        var result = ComputeCompanyDestination(world, npc, home, bonds);
        _company = (world, npc.Id.Value, now, npc.CurrentLocation, result);
        return result;
    }

    [ThreadStatic] private static (WorldState? World, long Npc, long Tick, CellCoord From, CellCoord? Result) _company;

    /// <summary>Por que quem conversa sozinho não sai atrás de alguém (AD-186; só a medida conta): fora de casa, sem falta de gente,
    /// ainda sem solidão, ou passa nas três (e então faltou aonde ir).</summary>
    internal enum SeekGate : byte { Open, Away, Content, NotLonely }

    internal static SeekGate WhyNotSeeking(WorldState world, Npc npc, LivingWorld.Domain.Population.Family.Household home, long now)
    {
        if (npc.CurrentLocation != home.Location) return SeekGate.Away;
        if (100 - npc.SocialAt(now) <= world.NeedsRules.UrgencyThreshold) return SeekGate.Content;
        // e só quem já se sente só (a solidão das conversas sem ninguém): uma noite sozinho em casa não tira ninguém de casa
        if (LivingWorld.Simulation.Cognition.PsycheMechanic.AffectOf(world, npc, now).Loneliness < world.NeedsRules.Psyche.SeekCompanyLoneliness)
            return SeekGate.NotLonely;
        return SeekGate.Open;
    }

    /// <summary>O destino da ida atrás de companhia, escolhido na saída e guardado no intento <c>Socialize</c> ("x,y").</summary>
    internal static CellCoord? CompanyTargetOf(Npc npc)
    {
        if (npc.CurrentIntent != ActionType.Socialize || npc.IntentStatus != IntentStatus.Active || npc.IntentTarget is not { } t) return null;
        int comma = t.IndexOf(',');
        return comma > 0 && int.TryParse(t.AsSpan(0, comma), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int x)
            && int.TryParse(t.AsSpan(comma + 1), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int y)
            ? new CellCoord(x, y) : null;
    }

    /// <summary>Na saída atrás de companhia (AD-186): guarda no intento o destino que a escolha deste tick deu, para a viagem inteira.
    /// Não apaga um plano de comida em curso (aí a ida não tem destino e se desfaz).</summary>
    internal static void PinCompanyTarget(WorldState world, Npc npc, long now)
    {
        var last = _company;
        if (!ReferenceEquals(last.World, world) || last.Npc != npc.Id.Value || last.Tick != now || last.Result is not { } cell) return;
        if (npc.IntentStatus == IntentStatus.Active && npc.CurrentIntent is ActionType.Buy or ActionType.Eat) return;
        npc.SetIntent(ActionType.Socialize, now,
            cell.X.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + cell.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static CellCoord? ComputeCompanyDestination(WorldState world, Npc npc, LivingWorld.Domain.Population.Family.Household home,
        LivingWorld.Domain.Population.Family.SocialBondRules bonds)
    {
        var here = npc.CurrentLocation;
        var psyche = world.NeedsRules.Psyche;
        if (SomeoneWithin(world, npc, here, bonds.SocializeRadius)) return null;

        CellCoord? best = null;
        double bestAffection = 0;
        long bestId = long.MaxValue;
        var targets = world.RelationshipTargetsOf(npc.Id);
        var owned = world.RelationshipsFrom(npc.Id);
        for (int i = 0; i < targets.Count; i++)
        {
            double affection = owned![i].Affection;
            if (affection <= 0 || affection < bestAffection || (affection == bestAffection && targets[i].Value > bestId)) continue;
            if (world.FindNpc(targets[i]) is not { IsAlive: true } friend || friend.City != npc.City) continue;
            // a visita é na casa do conhecido, quando ele está lá: o destino não foge durante a ida
            if (friend.Household is not { } fh || world.FindHousehold(fh) is not { IsHomeless: false } friendHome
                || friend.CurrentLocation != friendHome.Location) continue;
            var cell = friend.CurrentLocation;
            // perto de casa pela medida barata (células, sem rota): a rota de cada amigo enchia a memória do alcance da despensa
            if (cell == here || Math.Max(Math.Abs(cell.X - home.Location.X), Math.Abs(cell.Y - home.Location.Y)) > psyche.SeekCompanyMaxCells) continue;
            (best, bestAffection, bestId) = (cell, affection, friend.Id.Value);
        }
        if (best is not null) return best;
        if (world.FindCity(npc.City) is not { } city || city.Location == here
            || Math.Max(Math.Abs(city.Location.X - home.Location.X), Math.Abs(city.Location.Y - home.Location.Y)) > psyche.SeekCompanyMaxCells
            || !SomeoneWithin(world, npc, city.Location, bonds.SocializeRadius)) // ao largo só se há gente lá: ninguém sai para a praça vazia
            return null;
        return city.Location;
    }

    /// <summary>AD-186: quem quer conversar e está sozinho, para a leitura do cérebro: o conhecido que iria procurar (−1 = o largo), ou
    /// nulo se tem alguém perto ou não vai a lugar nenhum. Só lê.</summary>
    public static long? CompanySought(WorldState world, Npc npc)
    {
        if (CompanyDestinationOf(world, npc) is not { } cell) return null;
        var targets = world.RelationshipTargetsOf(npc.Id);
        long best = -1;
        double bestAffection = 0;
        var owned = world.RelationshipsFrom(npc.Id);
        for (int i = 0; i < targets.Count; i++)
            if (world.FindNpc(targets[i]) is { IsAlive: true } friend && friend.CurrentLocation == cell && owned![i].Affection > bestAffection)
                (best, bestAffection) = (friend.Id.Value, owned[i].Affection);
        return best;
    }

    private static bool SomeoneWithin(WorldState world, Npc npc, CellCoord here, int radius)
    {
        var everyone = world.AliveNpcIndex.Alive;
        int side = 2 * radius + 1;
        if (everyone.Count <= 2 * side * side)
        {
            for (int i = 0; i < everyone.Count; i++)
            {
                var other = everyone[i];
                if (other.Id != npc.Id && other.City == npc.City
                    && Math.Abs(other.CurrentLocation.X - here.X) <= radius && Math.Abs(other.CurrentLocation.Y - here.Y) <= radius)
                    return true;
            }
            return false;
        }
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var bucket = world.AliveNpcIndex.At(new CellCoord(here.X + dx, here.Y + dy));
                for (int i = 0; i < bucket.Count; i++)
                    if (bucket[i].Id != npc.Id && bucket[i].City == npc.City) return true;
            }
        return false;
    }

    private static CellCoord? RelocationDestinationOf(WorldState world, Npc npc) =>
        npc.Household is { } householdId
        && world.FindHousehold(householdId) is { PendingRelocationCity: { } cityId }
        && world.FindCity(cityId) is { } city
            ? city.Location
            : null;
}
