using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Behavior.Resolvers;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — #242: o que a pessoa faz com o tempo livre (depois do turno, sem emprego): vai para casa, ao largo, ao
// mercado, à casa de quem conhece ou dá um passeio curto, com destino real e viagem em metros, em vez de ficar à toa onde estava.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>O motivo da viagem que o tempo livre guarda: conversar ou ficar à toa (as duas vão aonde a escolha mandou).</summary>
    internal static bool IsOutingPurpose(WorldState world, ActionType? purpose) =>
        purpose == ActionType.Socialize || (purpose == ActionType.Idle && world.StreetRules.AfterWorkOutings);

    /// <summary>#242: aonde vai quem escolheu <paramref name="candidate"/> (à toa ou conversar): a ida atrás de companhia (AD-186) vale primeiro
    /// (só para quem conversa); sem ela, o destino do tempo livre. Nulo: fica onde está. A ida em curso segue o destino guardado no intento.</summary>
    internal static CellCoord? OutingDestinationOf(WorldState world, Npc npc, ActionType candidate, MarketIndex marketIndex)
    {
        if (!world.StreetRules.AfterWorkOutings)
            return candidate == ActionType.Socialize ? CompanyDestinationOf(world, npc) : null; // como antes: só a ida atrás de companhia
        // a ida em curso (ou recém-chegada) de um passeio segue o destino guardado no intento; a viagem por outro motivo que chegou (o posto) decide de novo
        if (npc.CurrentAction == ActionType.Travel && npc.TravelPurpose is ActionType.Socialize or ActionType.Idle) return CompanyTargetOf(npc);
        if (candidate == ActionType.Socialize && CompanyDestinationOf(world, npc) is { } company) return company;
        // quem quer conversar e já tem gente por perto (os colegas do posto, a família em casa) fica na conversa, na maior parte das vezes: a
        // viagem (de uma hora inteira no passo de hora cheia) comeria a janela da conversa; o resto da gente vai aonde a escolha mandou
        if (candidate == ActionType.Socialize && world.FamilyRules.Bonds is { } bonds && SomeoneWithin(world, npc, npc.CurrentLocation, bonds.SocializeRadius)
            && world.Rng.DrawFor("outing-stay", unchecked(npc.Id.Value * 1_000_003L + world.CurrentDate.TotalHours / Math.Max(1, world.StreetRules.OutingBlockHours)))
               < world.StreetRules.OutingStayWithCompany)
            return null;
        return LeisureDestinationOf(world, npc, marketIndex);
    }

    /// <summary>#242: a rotina da hora é trabalho (o à toa veio de humor, corpo ou cuidado): não sai a passear. Posto por quem decide, só na escolha.</summary>
    [ThreadStatic] private static bool _onDuty;

    [ThreadStatic] private static (WorldState? World, long Npc, long Tick, CellCoord From, CellCoord? Result) _leisure;

    private enum LeisureKind : byte { Home, Square, Market, Friend, Stroll }

    private static CellCoord? LeisureDestinationOf(WorldState world, Npc npc, MarketIndex marketIndex)
    {
        var rules = world.StreetRules;
        long now = world.CurrentDate.TotalHours;
        int hour = world.CurrentDate.Hour;
        if (_onDuty || hour < rules.OutingFromHour || hour > rules.OutingUntilHour) return null;
        var last = _leisure;
        if (ReferenceEquals(last.World, world) && last.Npc == npc.Id.Value && last.Tick == now && last.From == npc.CurrentLocation) return last.Result;
        var result = ComputeLeisureDestination(world, npc, marketIndex, now, hour);
        _leisure = (world, npc.Id.Value, now, npc.CurrentLocation, result);
        return result;
    }

    private static CellCoord? ComputeLeisureDestination(WorldState world, Npc npc, MarketIndex marketIndex, long now, int hour)
    {
        var rules = world.StreetRules;
        // criança e idoso frágil não saem sozinhos; a casa de luto e quem cuida de doente fica em casa; o corpo de quem joga é do jogador
        if (npc.ControllerId is not null || world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LifeStage.Child) return null;
        if (!world.OntogenyRules.IsEligible(npc.Development, ActionType.Travel)) return null;
        if (world.IsCaregiver(npc.Id) && LivingWorld.Simulation.Health.SickBehavior.IsCareDay(world, npc.Id, now)) return null;
        if (npc.Psyche is { } psyche && now < psyche.CalmFromTick
            && LivingWorld.Simulation.Cognition.PsycheMechanic.SocialWithdrawal(world, npc, now) > 0) return null;

        var home = npc.Household is { } hh ? world.FindHousehold(hh) : null;
        CellCoord anchor = home is { IsHomeless: false } ? home.Location : npc.CurrentLocation;
        int maxCells = world.NeedsRules.Psyche.SeekCompanyMaxCells;
        double tired = 100 - npc.SleepAt(now);
        double lonely = 100 - npc.SocialAt(now);
        bool evening = hour >= rules.OutingHomewardHour;

        Span<(LeisureKind Kind, CellCoord Cell, double Weight)> options = stackalloc (LeisureKind, CellCoord, double)[5];
        int count = 0;
        if (home is { IsHomeless: false }) options[count++] = (LeisureKind.Home, home.Location, 20 + tired / 2 + (evening ? 30 : 0));

        if (world.FindCity(npc.City) is { } city && Near(city.Location, anchor, maxCells))
            options[count++] = (LeisureKind.Square, city.Location, 10 + lonely / 2 - (evening ? 6 : 0));

        if (marketIndex.NearestTo(anchor) is { } market && Near(market.Location, anchor, maxCells)
            && (world.FindCity(npc.City) is not { } c2 || market.Location != c2.Location))
            options[count++] = (LeisureKind.Market, market.Location, evening ? 3 : 6 + lonely / 4);

        if (FriendHome(world, npc, anchor, maxCells) is { } friendHome && friendHome != anchor)
            options[count++] = (LeisureKind.Friend, friendHome, 10 + lonely / 2 - (evening ? 6 : 0));

        long key = unchecked(npc.Id.Value * 1_000_003L + now / Math.Max(1, rules.OutingBlockHours));
        if (!evening && StrollTarget(world, npc, anchor, key) is { } stroll)
            options[count++] = (LeisureKind.Stroll, stroll, 15);

        if (count == 0) return null;
        // quem já está num dos lugares de folga (em casa, no largo, na casa do conhecido) fica, na maior parte das vezes, até o bloco virar:
        // trocar de lugar a cada despertar é andar o dia todo (e, de hora em hora, uma hora de viagem cada)
        for (int i = 0; i < count; i++)
            if (options[i].Cell == npc.CurrentLocation && options[i].Kind != LeisureKind.Stroll
                && world.Rng.DrawFor("outing-stay2", key) < rules.OutingStayWithCompany) return null;
        double total = 0;
        for (int i = 0; i < count; i++) total += options[i].Weight;
        double pick = world.Rng.DrawFor("outing", key) * total;
        for (int i = 0; i < count; i++)
        {
            pick -= options[i].Weight;
            if (pick < 0 || i == count - 1) return options[i].Cell;
        }
        return null;
    }

    private static bool Near(CellCoord a, CellCoord b, int cells) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= cells;

    /// <summary>A casa de quem a pessoa mais gosta (afeto acima de zero; empate pelo menor id) na mesma cidade, perto o bastante de casa.
    /// Não depende de o conhecido estar lá: a escolha vale o bloco todo.</summary>
    private static CellCoord? FriendHome(WorldState world, Npc npc, CellCoord anchor, int maxCells)
    {
        var targets = world.RelationshipTargetsOf(npc.Id);
        if (targets.Count == 0) return null;
        var owned = world.RelationshipsFrom(npc.Id);
        CellCoord? best = null;
        double bestAffection = 0;
        long bestId = long.MaxValue;
        for (int i = 0; i < targets.Count; i++)
        {
            double affection = owned![i].Affection;
            if (affection <= 0 || affection < bestAffection || (affection == bestAffection && targets[i].Value > bestId)) continue;
            if (world.FindNpc(targets[i]) is not { IsAlive: true } friend || friend.City != npc.City) continue;
            if (friend.Household is not { } fh || world.FindHousehold(fh) is not { IsHomeless: false } friendHome || !Near(friendHome.Location, anchor, maxCells)) continue;
            (best, bestAffection, bestId) = (friendHome.Location, affection, friend.Id.Value);
        }
        return best;
    }

    /// <summary>O passeio curto: uma célula a de <c>OutingStrollMinCells</c> a <c>OutingStrollMaxCells</c> da casa, numa das oito direções
    /// (a primeira que o mapa deixa), alcançável a pé. Sem sorteio guardado: sai da chave da pessoa e do bloco.</summary>
    private static CellCoord? StrollTarget(WorldState world, Npc npc, CellCoord anchor, long key)
    {
        var rules = world.StreetRules;
        int min = Math.Max(1, rules.OutingStrollMinCells), max = Math.Max(min, rules.OutingStrollMaxCells);
        double u = world.Rng.DrawFor("outing-stroll", key);
        int distance = min + Math.Min(max - min, (int)(u * (max - min + 1)));
        int start = Math.Min(7, (int)(world.Rng.DrawFor("outing-dir", key) * 8));
        for (int i = 0; i < 8; i++)
        {
            int d = (start + i) % 8;
            int dx = d switch { 0 or 1 or 7 => 1, 3 or 4 or 5 => -1, _ => 0 };
            int dy = d switch { 1 or 2 or 3 => 1, 5 or 6 or 7 => -1, _ => 0 };
            var cell = new CellCoord(anchor.X + dx * distance, anchor.Y + dy * distance);
            if (!world.Map.TryGetCell(cell, out _) || world.IsExtraordinaryConstructCell(cell)) continue;
            if (!RestPlaceResolver.IsReachable(world.Map, anchor, cell)) continue;
            return cell;
        }
        return null;
    }
}

