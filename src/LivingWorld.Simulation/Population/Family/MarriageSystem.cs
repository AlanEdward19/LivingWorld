using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Casamento monogâmico — helper estático reusado por
/// <see cref="CourtshipSystem"/>.</summary>
public static class MarriageSystem
{
    public static void Marry(WorldState world, TickContext ctx, Npc spouseA, Npc spouseB)
    {
        var previousA = spouseA.Household;
        var previousB = spouseB.Household;
        // AD-184: onde cada um morava (a casa e a célula), para o casal morar onde dá até a casa nova ficar pronta
        var homeA = previousA is { } pa && world.FindHousehold(pa) is { Dwelling: { } da } ha ? (da, ha.Location) : ((BuildingId, CellCoord)?)null;
        var homeB = previousB is { } pb && world.FindHousehold(pb) is { Dwelling: { } db } hb ? (db, hb.Location) : ((BuildingId, CellCoord)?)null;
        // o que cada um leva de casa (AD-136, achado 18): nada é criado no casamento
        var stock = new Dictionary<ResourceType, long>();
        TakeShare(world, spouseA, stock);
        TakeShare(world, spouseB, stock);
        LeavePreviousHousehold(world, ctx, spouseA);
        LeavePreviousHousehold(world, ctx, spouseB);

        var head = spouseA.Id.Value <= spouseB.Id.Value ? spouseA : spouseB;
        var other = ReferenceEquals(head, spouseA) ? spouseB : spouseA;

        var householdId = world.NextHouseholdIdAndAdvance();
        var household = new Household(
            householdId, head.CurrentLocation, head.Id, [head.Id, other.Id], stock, city: head.City);
        world.AddHousehold(household);

        head.JoinHousehold(householdId);
        other.JoinHousehold(householdId);
        BringDependents(world, ctx, previousA, spouseA, household);
        BringDependents(world, ctx, previousB, spouseB, household);
        SettleWhereTheyCan(world, household, ReferenceEquals(head, spouseA) ? (previousA, homeA) : (previousB, homeB),
            ReferenceEquals(head, spouseA) ? (previousB, homeB) : (previousA, homeA));
        spouseA.Marry(spouseB.Id);
        spouseB.Marry(spouseA.Id);
        LivingWorld.Simulation.Population.Naming.PersonNaming.OnMarriage(world, spouseA, spouseB); // AD-178: costume do nome
        BeginUnion(world, spouseA);
        BeginUnion(world, spouseB);

        ctx.LogEvent(WorldEventKind.Marriage, $"{spouseA.Id.Value}|{spouseB.Id.Value}", sourceSystem: "MarriageSystem");
    }

    /// <summary>A parte de quem sai de casa para casar: tudo, se mora sozinho; senão a parte dele (estoque ÷ moradores
    /// vivos), no máximo <see cref="FamilyRules.MarriageInitialStock"/> por recurso quando o cenário declara teto. O que sai
    /// da despensa de origem entra na do casal: o estoque do mundo não muda.</summary>
    private static void TakeShare(WorldState world, Npc npc, Dictionary<ResourceType, long> into)
    {
        if (npc.Household is not { } id || world.FindHousehold(id) is not { } home) return;
        int living = 0;
        foreach (var memberId in home.Members)
            if (world.FindNpc(memberId) is { IsAlive: true }) living++;
        if (living == 0) return;
        foreach (var (resource, amount) in home.Stock.OrderBy(kv => kv.Key.Id).ToList())
        {
            long share = living == 1 ? amount : amount / living;
            if (living > 1 && world.FamilyRules.MarriageInitialStock.TryGetValue(resource.Id, out long cap))
                share = Math.Min(share, cap);
            if (share <= 0 || !home.Withdraw(resource, share).IsSuccess) continue;
            into[resource] = into.GetValueOrDefault(resource) + share;
        }
    }

    /// <summary>AD-184 (casa nova é obra): o casal mora onde dá até a casa dele ficar pronta. A casa que um deles deixou vazia (morava
    /// sozinho) fica com o casal; senão eles ficam de favor na casa de onde o chefe saiu (ou o outro), apertados com a família de lá,
    /// e a obra da casa nova começa (<see cref="LivingWorld.Simulation.Cities.Works.BuildWorks"/>). Sem a regra (ou sem rua de
    /// verdade), o casal fica na célula onde estava, como antes.</summary>
    private static void SettleWhereTheyCan(WorldState world, Household household,
        (HouseholdId? Id, (BuildingId Dwelling, CellCoord Location)? Home) first, (HouseholdId? Id, (BuildingId Dwelling, CellCoord Location)? Home) second)
    {
        if (!world.StreetsOn || !world.StreetRules.HouseIsWork) return;
        // das casas possíveis, a mais conveniente para o casal (água, mercado, o trabalho de cada um), não a primeira
        var members = household.Members.Select(world.FindNpc).Where(n => n is { IsAlive: true }).Cast<Npc>().ToList();
        var places = LivingWorld.Simulation.Cities.Works.HomePlaces.Of(world, household.City, members);
        (BuildingId Dwelling, CellCoord Location)? Best(bool vacated)
        {
            (BuildingId, CellCoord)? best = null;
            double bestCost = double.MaxValue;
            foreach (var (id, home) in new[] { first, second })
            {
                if (home is not { } h || world.FindBuilding(h.Dwelling) is null) continue;
                bool empty = id is not { } left || world.FindHousehold(left) is null or { IsEmpty: true };
                if (vacated && !empty) continue;
                double cost = places.YearlyCost(world, h.Location);
                if (cost < bestCost) { best = h; bestCost = cost; }
            }
            return best;
        }
        if (Best(vacated: true) is { } own)
        {
            household.MoveIn(own.Dwelling, own.Location); // a casa ficou vazia: é deles
            return;
        }
        if (Best(vacated: false) is { } favor)
        {
            household.MoveIn(favor.Dwelling, favor.Location); // de favor com a família de onde saiu
            world.Ground!.Lodgers.Add(household.Id.Value);
        }
    }

    /// <summary>Início da união no estado conjugal da pessoa (AD-136): sem mágoa, sem caso, uma união a mais.</summary>
    private static void BeginUnion(WorldState world, Npc npc)
    {
        if (npc.Union?.AffairWith is { } lover && world.FindNpc(new NpcId(lover)) is { Union: { } theirs } other
            && theirs.AffairWith == npc.Id.Value)
            other.SetUnion(theirs with { AffairWith = null });
        var previous = npc.Union ?? new UnionState();
        npc.SetUnion(previous with
        {
            SinceTick = world.CurrentDate.TotalHours,
            Grievance = 0,
            AffairWith = null,
            Unions = previous.Unions + 1,
        });
    }

    /// <summary>Quem casa leva consigo os filhos pequenos que ficariam sem ninguém (ou que são seus): sem isso a criança do domicílio de um adulto
    /// único era deixada sozinha ao casamento e morria de sede (dívida da Fase 21).</summary>
    private static void BringDependents(WorldState world, TickContext ctx, HouseholdId? previousId, Npc leaving, Household destination)
    {
        if (previousId is not { } id || world.FindHousehold(id) is not { } previous) return;
        var rules = world.LifeStageRules;
        var now = world.CurrentDate;
        bool someoneRemains = HouseholdRedistribution.HasLivingAdultOrElder(world, previous, rules, now);

        var kids = previous.Members
            .Select(world.FindNpc)
            .Where(n => n is { IsAlive: true } && rules.LifeStageOf(n.AgeYears(now)) == LivingWorld.Domain.Population.Body.LifeStage.Child
                && (!someoneRemains || n.MotherId == leaving.Id || n.FatherId == leaving.Id))
            .Cast<Npc>()
            .OrderBy(n => n.Id.Value)
            .ToList();
        foreach (var kid in kids)
        {
            previous.RemoveMember(kid.Id);
            destination.AddMember(kid.Id);
            kid.JoinHousehold(destination.Id);
        }
        // casa que ficou vazia: a despensa vai com quem saiu dela, não se perde (AD-136)
        if (previous.IsEmpty)
            foreach (var (resource, amount) in previous.Stock.OrderBy(kv => kv.Key.Id).ToList())
                if (amount > 0 && previous.Withdraw(resource, amount).IsSuccess) destination.Deposit(resource, amount);
        HouseholdCleanup.DissolveIfEmpty(world, ctx, previous);
    }

    private static void LeavePreviousHousehold(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Household is not { } householdId) return;

        var household = world.FindHousehold(householdId);
        household?.RemoveMember(npc.Id);
        npc.LeaveHousehold(world.CurrentDate);
        if (household is not null)
            HouseholdCleanup.DissolveIfEmpty(world, ctx, household);
    }
}
