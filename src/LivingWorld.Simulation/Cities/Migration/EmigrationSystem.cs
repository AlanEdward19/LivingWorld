using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Migration;

/// <summary>Quem não tem lugar vai embora (AD-145), <c>Monthly</c>. O adulto jovem solteiro, sem filho e sem gravidez, sem
/// trabalho e sem nenhuma vaga paga na cidade dele, decide partir no mês com a chance de <see cref="EmigrationRules"/> pesada
/// pelo próprio temperamento (abertura e ambição empurram, aversão a risco e laços seguram). Vai para outra cidade do mundo com
/// vaga paga, viajando como a migração de casa (<see cref="RelocationArrivalSystem"/> conclui); sem nenhuma, sai do mundo
/// simulado: deixa o dinheiro com quem fica, a vaga na casa, e não volta. Não é morte: evento <see cref="WorldEventKind.Emigrated"/>.
/// O <see cref="MigrationSystem"/> continua movendo a casa inteira; este move a pessoa que a casa não sustenta.</summary>
[LivingCapability("EMIGRATION", Events = [WorldEventKind.Emigrated], Consumers = ["timeline.emigration"])]
[RunsAfter(typeof(EmploymentSystem))]
public sealed class EmigrationSystem : ISimulationSystem
{
    public const string SystemName = "population-emigration";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (world.FamilyRules.Pressure?.Emigration is not { } rules) return;
        if (!rules.Enabled || !world.EconomyRules.Enabled || rules.DecisionPerYear <= 0) return;

        var now = world.CurrentDate;
        List<Npc>? leaving = null;
        Dictionary<CityId, bool>? openByCity = null;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (!MayLeave(world, rules, npc, now)) continue;
            openByCity ??= OpenCities(world);
            if (openByCity.GetValueOrDefault(npc.City)) continue; // há vaga paga na cidade: a vida está aqui
            double chance = rules.DecisionPerYear / now.Calendar.MonthsPerYear * Drive(world, rules, npc);
            if (ctx.StreamFor("emigration", unchecked(npc.Id.Value * 1_000_003L + ctx.CurrentTick)).NextDouble() >= chance) continue;
            (leaving ??= []).Add(npc);
        }

        if (leaving is null) return;
        foreach (var npc in leaving)
        {
            var destination = DestinationFor(world, npc, openByCity!);
            if (destination is { } city) MoveTo(world, ctx, npc, city);
            else LeaveWorld(world, ctx, npc);
        }
    }

    /// <summary>Solteiro sem cônjuge vivo, sem cortejo nem gravidez, na idade de partir, adulto, sem trabalho, e sem ninguém da
    /// casa que dependa dele (filho dele na casa, ou casa sem outro adulto com criança).</summary>
    private static bool MayLeave(WorldState world, EmigrationRules rules, Npc npc, WorldDate now)
    {
        if (npc.Employer is not null || npc.CourtingWith is not null || npc.PregnantUntil is not null) return false;
        int age = npc.AgeYears(now);
        if (age < rules.MinAgeYears || age > rules.MaxAgeYears) return false;
        if (world.LifeStageRules.LifeStageOf(age) != LifeStage.Adult) return false;
        if (npc.Spouse is { } spouseId && world.FindNpc(spouseId) is { IsAlive: true }) return false;
        if (npc.Household is not { } homeId || world.FindHousehold(homeId) is not { } home) return true;
        bool otherGrown = false, child = false;
        foreach (var memberId in home.Members)
        {
            if (memberId == npc.Id || world.FindNpc(memberId) is not { IsAlive: true } member) continue;
            if (world.LifeStageRules.LifeStageOf(member.AgeYears(now)) == LifeStage.Child)
            {
                if (member.MotherId == npc.Id || member.FatherId == npc.Id) return false;
                child = true;
            }
            else otherGrown = true;
        }
        return otherGrown || !child;
    }

    /// <summary>Vontade de partir (≈ 1 no temperamento médio sem laços): abertura e ambição empurram, aversão a risco segura, e
    /// os laços (pais vivos na cidade, o maior afeto por alguém dela) seguram até <see cref="EmigrationRules.TiesHold"/>.</summary>
    internal static double Drive(WorldState world, EmigrationRules rules, Npc npc)
    {
        var p = npc.Personality;
        double push = (p.Openness + p.Ambition) / 100.0 * (1.5 - p.RiskAversion / 100.0);
        double ties = 0;
        foreach (var parentId in new[] { npc.MotherId, npc.FatherId })
            if (parentId is { } id && world.FindNpc(id) is { IsAlive: true } parent && parent.City == npc.City) ties = 0.5;
        var relations = world.RelationshipsFrom(npc.Id);
        if (relations is not null)
            foreach (var relation in relations)
                ties = Math.Max(ties, relation.Affection / 100.0);
        return push * (1 - rules.TiesHold * Math.Clamp(ties, 0, 1));
    }

    /// <summary>Cidades com alguma vaga paga (para qualquer ofício que a oficina emprega).</summary>
    private static Dictionary<CityId, bool> OpenCities(WorldState world)
    {
        var open = new Dictionary<CityId, bool>();
        foreach (var workplace in world.Workplaces)
        {
            if (open.GetValueOrDefault(workplace.City)) continue;
            if (workplace.Employees.Count >= workplace.MaxVacancies || workplace.PayrollFailed) continue;
            open[workplace.City] = true;
        }
        return open;
    }

    /// <summary>A cidade ativa de menor id, que não seja a dele, com vaga paga; só com cidades ligadas.</summary>
    private static CityId? DestinationFor(WorldState world, Npc npc, Dictionary<CityId, bool> open)
    {
        if (!world.CityRules.Enabled) return null;
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
            if (city.Id != npc.City && open.GetValueOrDefault(city.Id)) return city.Id;
        return null;
    }

    /// <summary>Parte para outra cidade do mundo: casa própria de um só, levando o dinheiro, e a viagem da migração.</summary>
    private static void MoveTo(WorldState world, TickContext ctx, Npc npc, CityId destination)
    {
        LeaveHome(world, ctx, npc);
        var household = new Household(world.NextHouseholdIdAndAdvance(), npc.CurrentLocation, npc.Id, [npc.Id], city: npc.City);
        world.AddHousehold(household);
        npc.JoinHousehold(household.Id);
        household.BeginRelocation(destination);
        npc.SetCurrentAction(ActionType.Travel, ctx.CurrentTick);
        ctx.LogEvent(WorldEventKind.Emigrated, $"{npc.Id.Value}|{destination.Value}", sourceSystem: SystemName);
    }

    /// <summary>Sai do mundo simulado: o dinheiro fica com quem ele deixa (um adulto da casa, senão os herdeiros); o caso
    /// que tivesse com alguém acaba; a vaga na casa se fecha. A moeda do mundo não muda.</summary>
    private static void LeaveWorld(WorldState world, TickContext ctx, Npc npc)
    {
        long savings = npc.Wallet.Amount;
        if (savings > 0 && Keeper(world, npc) is { } keeper && npc.TryDebitWallet(new Money(savings)).IsSuccess)
            keeper.CreditWallet(new Money(savings));
        if (npc.Union?.AffairWith is { } lover && world.FindNpc(new NpcId(lover)) is { Union: { } theirs } other && theirs.AffairWith == npc.Id.Value)
            other.SetUnion(theirs with { AffairWith = null });

        npc.LeaveWorld(world.CurrentDate);
        world.AliveNpcIndex.OnDied(npc);
        world.ForgetHealthOf(npc.Id);
        ctx.LogEvent(WorldEventKind.Emigrated, $"{npc.Id.Value}|fora", sourceSystem: SystemName);
        if (npc.Household is { } homeId && world.FindHousehold(homeId) is { } home)
        {
            home.RemoveMember(npc.Id);
            HouseholdCleanup.DissolveIfEmpty(world, ctx, home);
        }
    }

    /// <summary>Com quem fica o dinheiro: o adulto da casa de menor id; sem ninguém, o primeiro herdeiro da ordem do espólio.</summary>
    private static Npc? Keeper(WorldState world, Npc npc)
    {
        if (npc.Household is { } homeId && world.FindHousehold(homeId) is { } home)
            foreach (var memberId in home.Members.OrderBy(id => id.Value))
                if (memberId != npc.Id && world.FindNpc(memberId) is { IsAlive: true } member
                    && world.LifeStageRules.LifeStageOf(member.AgeYears(world.CurrentDate)) != LifeStage.Child)
                    return member;
        return EstateSystem.HeirsOf(world, npc).FirstOrDefault();
    }

    private static void LeaveHome(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Household is not { } homeId || world.FindHousehold(homeId) is not { } home) return;
        home.RemoveMember(npc.Id);
        npc.LeaveHousehold(world.CurrentDate);
        HouseholdCleanup.DissolveIfEmpty(world, ctx, home);
    }
}
