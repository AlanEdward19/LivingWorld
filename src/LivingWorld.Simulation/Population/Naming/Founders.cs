using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>Quem funda a cidade-filha (AD-183). Regra emergente, sem sorteio: entre os adultos das casas que saem da mãe para o
/// aglomerado, lidera quem leva mais gente consigo (a própria casa e as casas de pais, filhos e irmãos que saem junto), depois
/// os meios (carteira), a idade de quem já tem voz sem ter perdido o vigor e a boa fama vista; empate pelo menor id. O
/// fundador fica na cidade (<c>City.FounderId</c>), o feito vira boa fama dele (e da linhagem) e a fundação vai ao log.</summary>
public static class Founders
{
    /// <summary>Peso dos meios, da idade e da boa fama em relação à gente que leva (que pesa 1).</summary>
    private const double MeansWeight = 0.5, AgeWeight = 0.5, StandingWeight = 0.5;

    /// <summary>Faixa de idade de quem lidera (anos): fora dela a idade pesa menos.</summary>
    private const int PrimeFrom = 25, PrimeTo = 55;

    /// <summary>As casas da mãe cujo responsável está dentro do aglomerado agora — as mesmas que a fundação muda de cidade.</summary>
    public static List<LivingWorld.Domain.Population.Family.Household> Leaving(WorldState world, CityId mother, CityBounds bounds)
    {
        var leaving = new List<LivingWorld.Domain.Population.Family.Household>();
        foreach (var household in world.Households.OrderBy(h => h.Id.Value))
            if (household.City == mother && world.FindNpc(household.Head) is { IsAlive: true } head && bounds.Contains(head.CurrentLocation))
                leaving.Add(household);
        return leaving;
    }

    public static Npc? Pick(WorldState world, IReadOnlyList<LivingWorld.Domain.Population.Family.Household> leaving, long now)
    {
        var adults = new List<(Npc Npc, int People)>();
        var movers = new HashSet<NpcId>();
        foreach (var household in leaving) foreach (var id in household.Members) movers.Add(id);
        foreach (var household in leaving)
            foreach (var id in household.Members.OrderBy(m => m.Value))
                if (world.FindNpc(id) is { IsAlive: true } npc
                    && world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult)
                    adults.Add((npc, household.Members.Count + KinHouseholds(world, npc, household, leaving)));
        if (adults.Count == 0) return null;

        int maxPeople = adults.Max(a => a.People);
        long maxMeans = Math.Max(1, adults.Max(a => a.Npc.Wallet.Amount));
        Npc? best = null;
        double bestScore = double.NegativeInfinity;
        foreach (var (npc, people) in adults.OrderBy(a => a.Npc.Id.Value))
        {
            int age = npc.AgeYears(world.CurrentDate);
            double ageFit = age < PrimeFrom ? age / (double)PrimeFrom : age > PrimeTo ? PrimeTo / (double)age : 1;
            double score = people / (double)maxPeople + MeansWeight * Math.Max(0, npc.Wallet.Amount) / maxMeans + AgeWeight * ageFit
                + StandingWeight * Honor.StandingOf(world, npc, now) / 100.0;
            if (score > bestScore) (bestScore, best) = (score, npc);
        }
        return best;
    }

    /// <summary>Gente das outras casas que sai junto e é da família dele (pais, filhos, irmãos na chefia de outra casa que sai).</summary>
    private static int KinHouseholds(WorldState world, Npc npc, LivingWorld.Domain.Population.Family.Household own,
        IReadOnlyList<LivingWorld.Domain.Population.Family.Household> leaving)
    {
        int people = 0;
        foreach (var other in leaving)
        {
            if (other.Id == own.Id || world.FindNpc(other.Head) is not { } head) continue;
            bool kin = head.MotherId == npc.Id || head.FatherId == npc.Id || npc.MotherId == head.Id || npc.FatherId == head.Id
                || (npc.MotherId is { } m && head.MotherId == m) || (npc.FatherId is { } f && head.FatherId == f);
            if (kin) people += other.Members.Count;
        }
        return people;
    }

    /// <summary>A fundação vai ao log (cidade, mãe, fundador, costume do nome) e o feito vira boa fama do fundador.</summary>
    public static void Record(WorldState world, TickContext ctx, City city, City mother, Npc? founder, string nameCustom)
    {
        ctx.LogEvent(WorldEventKind.SettlementFounded,
            $"{city.Id.Value}|{mother.Id.Value}|{founder?.Id.Value ?? -1}|{nameCustom}", sourceSystem: "SpatialSettlementFoundingSystem");
        if (founder is not null && world.FamilyRules.Names.HonorOn)
            Honor.Credit(world, founder.Id.Value, world.FamilyRules.Names.HonorForFounding, ctx.CurrentTick);
    }
}
