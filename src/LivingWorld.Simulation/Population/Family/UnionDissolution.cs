using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>O fim de uma união em vida (AD-136): quem sai, com quem ficam os filhos, o que cada um leva e para onde vai.
/// Filho só de quem sai vai com ele; filho só de quem fica, fica; filho dos dois fica com a mãe abaixo da idade tenra e,
/// acima, com o pai na fração da norma (sorteio por filho: irmãos podem ir para casas diferentes). No abandono quem foge vai
/// sozinho e sem nada, para outro povoado se houver. Quem sai vai para a casa dos pais ou de um irmão adulto, se houver, ou
/// abre casa própria; leva a parte da despensa das pessoas que vão com ele. O vínculo de cônjuge se desfaz dos dois lados e
/// cada um só pode unir-se de novo depois do tempo que a norma pede para aquele modo.</summary>
public static class UnionDissolution
{
    public static string Label(UnionEndMode mode) => mode switch
    {
        UnionEndMode.Abandonment => "abandono",
        UnionEndMode.Annulment => "anulacao",
        UnionEndMode.Divorce => "divorcio",
        _ => "separacao",
    };

    public static void Separate(WorldState world, TickContext ctx, Npc leaver, Npc stayer, UnionEndMode mode)
    {
        var rules = world.FamilyRules.Unions;
        var now = world.CurrentDate;
        var home = leaver.Household is { } id ? world.FindHousehold(id) : null;
        var children = new List<Npc>();
        int living = 0;
        if (home is not null)
        {
            var rng = ctx.StreamFor("union-custody", unchecked(leaver.Id.Value * 1_000_003L + ctx.CurrentTick));
            foreach (var memberId in home.Members.OrderBy(m => m.Value).ToList())
            {
                if (world.FindNpc(memberId) is not { IsAlive: true } member) continue;
                living++;
                if (member.Id == leaver.Id || member.Id == stayer.Id || mode == UnionEndMode.Abandonment) continue;
                if (world.LifeStageRules.LifeStageOf(member.AgeYears(now)) != LifeStage.Child) continue;
                bool ofLeaver = member.MotherId == leaver.Id || member.FatherId == leaver.Id;
                bool ofStayer = member.MotherId == stayer.Id || member.FatherId == stayer.Id;
                if (!ofLeaver) continue;
                bool goes = !ofStayer || (member.AgeYears(now) < rules.TenderYearsAge
                    ? leaver.Sex == Sex.Female
                    : (rng.NextDouble() < rules.OlderChildrenToFather) == (leaver.Sex == Sex.Male));
                if (goes) children.Add(member);
            }
        }

        var destination = Destination(world, ctx, leaver, home, children.Count == 0, mode);
        Move(world, ctx, leaver, home, destination);
        foreach (var child in children) Move(world, ctx, child, home, destination);

        if (home is not null && mode != UnionEndMode.Abandonment && living > 0)
        {
            double share = rules.LeaverStockShare * (1 + children.Count) / living;
            foreach (var (resource, amount) in home.Stock.OrderBy(kv => kv.Key.Id).ToList())
            {
                long take = (long)(amount * share);
                if (take > 0 && home.Withdraw(resource, take).IsSuccess) destination.Deposit(resource, take);
            }
        }

        leaver.EndMarriage();
        stayer.EndMarriage();
        long courtFrom = now.TotalHours + (long)rules.RemarryYears(mode) * now.Calendar.HoursPerYear;
        foreach (var (self, other) in new[] { (leaver, stayer), (stayer, leaver) })
            self.SetUnion((self.Union ?? new UnionState()) with
            {
                Grievance = 0,
                FormerSpouse = other.Id.Value,
                EndMode = mode,
                CourtFromTick = courtFrom,
            });

        ctx.LogEvent(WorldEventKind.UnionEnded,
            $"{leaver.Id.Value}|{stayer.Id.Value}|{Label(mode)}|{destination.Id.Value}", sourceSystem: UnionSystem.SystemName);
    }

    /// <summary>Para onde vai quem sai: casa da mãe, do pai ou de um irmão adulto na mesma cidade; senão casa própria (no
    /// abandono, sempre casa própria, a caminho de outro povoado se o mundo tiver um).</summary>
    private static Household Destination(WorldState world, TickContext ctx, Npc leaver, Household? home, bool alone, UnionEndMode mode)
    {
        if (mode != UnionEndMode.Abandonment)
            foreach (var kin in KinHomes(world, leaver))
                if (kin.Id != home?.Id && kin.City == leaver.City) return kin;

        var household = new Household(world.NextHouseholdIdAndAdvance(), home?.Location ?? leaver.CurrentLocation, leaver.Id,
            [leaver.Id], city: leaver.City);
        world.AddHousehold(household);

        if (mode == UnionEndMode.Abandonment && alone && world.CityRules.Enabled)
        {
            var origin = world.FindCity(leaver.City);
            var away = world.ActiveCities().Where(c => c.Id != leaver.City)
                .OrderBy(c => origin is null ? 0 : Math.Abs(c.Location.X - origin.Location.X) + Math.Abs(c.Location.Y - origin.Location.Y))
                .ThenBy(c => c.Id.Value).FirstOrDefault();
            if (away is not null)
            {
                household.BeginRelocation(away.Id);
                leaver.SetCurrentAction(ActionType.Travel, ctx.CurrentTick);
            }
        }
        return household;
    }

    private static IEnumerable<Household> KinHomes(WorldState world, Npc npc)
    {
        foreach (var parentId in new[] { npc.MotherId, npc.FatherId })
            if (parentId is { } pid && world.FindNpc(pid) is { IsAlive: true, Household: { } h } && world.FindHousehold(h) is { } home)
                yield return home;
        foreach (var sibling in world.AliveNpcIndex.Alive)
        {
            if (sibling.Id == npc.Id || sibling.Household is not { } h) continue;
            bool same = (npc.MotherId is { } m && sibling.MotherId == m) || (npc.FatherId is { } f && sibling.FatherId == f);
            if (!same || world.LifeStageRules.LifeStageOf(sibling.AgeYears(world.CurrentDate)) == LifeStage.Child) continue;
            if (world.FindHousehold(h) is { } home) yield return home;
        }
    }

    private static void Move(WorldState world, TickContext ctx, Npc npc, Household? from, Household to)
    {
        if (from is not null && from.Id == to.Id) return;
        from?.RemoveMember(npc.Id);
        to.AddMember(npc.Id);
        npc.JoinHousehold(to.Id);
        if (from is not null) HouseholdCleanup.DissolveIfEmpty(world, ctx, from);
    }
}
