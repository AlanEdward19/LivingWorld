using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Streets;

/// <summary>Uma obra com turma (AD-179): o trecho de calçamento (<see cref="Paving"/>) ou o prédio na fila da cidade
/// (<see cref="Project"/>, com <see cref="City"/>).</summary>
internal readonly record struct WorkSite(PavingWork? Paving, City? City, ConstructionProject? Project)
{
    public List<long> Crew => Paving is not null ? Paving.Crew : Project!.Crew;
    public Guid CityId => Paving is not null ? Paving.City : City!.Id.Value;
    public string Label => Paving is not null ? Paving.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : $"predio-{Project!.BuildingTypeId}@{Project.SiteX}:{Project.SiteY}";
}

/// <summary>Trabalho na obra (AD-179): toda obra com turma é um lugar de trabalho de verdade — o calçamento e, com
/// <see cref="StreetRules.BuildNeedsCrew"/>, a casa ou o prédio da fila da cidade. O diarista é um adulto sem emprego que aceitou
/// pela utilidade dele (<see cref="OfferValue"/>: precisa de dinheiro ou de comida em casa, não tem emprego e a ida não come o
/// dia); vai e volta pela rota nas horas de trabalho da rotina (o destino de <c>Work</c> é a obra), cansa e sente fome como em
/// qualquer trabalho, e enquanto está lá não está em outra coisa. A obra só anda pelas horas de quem está NELA quando a jornada
/// termina ou é interrompida (<see cref="Clock"/>), até <see cref="StreetRules.CrewMax"/> braços; cada hora é paga na hora por
/// quem paga a obra. Sem dinheiro, quem trabalhou sem receber vai embora e a obra para.</summary>
public static class SiteWork
{
    public const string SourceSystem = "site-work";

    /// <summary>Há alguma obra que contrata (o caminho comum, sem obra, sai aqui sem custo).</summary>
    private static bool AnySite(WorldState world)
    {
        if (!world.StreetsOn) return false;
        if (world.Ground!.Works.Count > 0) return true;
        if (!world.StreetRules.BuildNeedsCrew || !world.CityRules.Enabled) return false;
        foreach (var city in world.ActiveCities())
            if (city.ConstructionQueue.Count > 0) return true;
        return false;
    }

    /// <summary>As obras que contratam agora, em ordem: os trechos por id, depois o primeiro prédio por pagar de cada cidade (o que
    /// a fila está construindo) com o lote já escolhido.</summary>
    internal static IEnumerable<WorkSite> Sites(WorldState world)
    {
        if (!world.StreetsOn) yield break;
        foreach (var work in world.Ground!.Works.OrderBy(w => w.Id)) yield return new WorkSite(work, null, null);
        if (!world.StreetRules.BuildNeedsCrew || !world.CityRules.Enabled) yield break;
        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
            if (city.ConstructionQueue.FirstOrDefault(p => p.TicksRemaining > 0) is { SiteX: >= 0 } project)
                yield return new WorkSite(null, city, project);
    }

    /// <summary>A obra de que o NPC é diarista; nula fora de obra.</summary>
    internal static WorkSite? WorkOf(WorldState world, Npc npc)
    {
        if (!AnySite(world)) return null;
        long id = npc.Id.Value;
        foreach (var work in world.Ground!.Works)
            if (work.Crew.Contains(id)) return new WorkSite(work, null, null);
        if (!world.StreetRules.BuildNeedsCrew || !world.CityRules.Enabled) return null;
        foreach (var city in world.ActiveCities())
            foreach (var project in city.ConstructionQueue)
                if (project.Crew.Contains(id)) return new WorkSite(null, city, project);
        return null;
    }

    /// <summary>A célula onde a turma trabalha: a primeira do trecho, ou o lote do prédio.</summary>
    internal static CellCoord SiteOf(WorldState world, WorkSite site) =>
        site.Paving is { } work ? world.Ground!.CellOf(work.Cells[0]) : new CellCoord(site.Project!.SiteX, site.Project.SiteY);

    internal static CellCoord SiteOf(WorldState world, PavingWork work) => SiteOf(world, new WorkSite(work, null, null));

    /// <summary>Para onde o diarista vai trabalhar; nulo se não é diarista.</summary>
    internal static CellCoord? DestinationOf(WorldState world, Npc npc) => WorkOf(world, npc) is { } site ? SiteOf(world, site) : null;

    /// <summary>Jornal de uma hora: o jornal do dia (<see cref="PavingDecision.DayWage"/>) pelas horas da jornada.</summary>
    internal static long HourWage(WorldState world) =>
        Math.Max(1, PavingDecision.DayWage(world) / Math.Max(1, world.StreetRules.CrewHoursPerDay));

    public static double OfferValue(WorldState world, Npc npc, PavingWork work) => OfferValue(world, npc, new WorkSite(work, null, null));

    /// <summary>Quanto a oferta de trabalho na obra pesa para o NPC (termo <c>SiteWork</c> da utilidade de trabalhar): zero para quem
    /// tem emprego, não é adulto ou mora fora da cidade; senão <see cref="StreetRules.SiteOfferMax"/> × a necessidade (o maior entre o
    /// que falta de moeda na casa para a reserva e o que falta na despensa) × a parte da jornada que sobra depois da ida e volta pela
    /// rota. A obra que paga quem precisa e fica perto atrai; longe demais, não paga a caminhada.</summary>
    internal static double OfferValue(WorldState world, Npc npc, WorkSite site)
    {
        var rules = world.StreetRules;
        if (npc.Employer is not null || npc.City.Value != site.CityId) return 0;
        if (world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) != LifeStage.Adult) return 0;
        var home = npc.Household is { } h ? world.FindHousehold(h) : null;
        var from = home?.Location ?? npc.CurrentLocation;
        double commute = StreetRoutes.CostOf(world, from, SiteOf(world, site)).Cost;
        double hours = rules.CrewHoursPerDay;
        double share = (hours - 2 * commute) / hours;
        if (!(share > 0)) return 0;
        long reserve = PavingDecision.DayWage(world) * rules.SiteReserveDays;
        long means = home is null ? npc.Wallet.Amount : PavingDecision.MeansOf(world, home.Id.Value);
        double money = reserve <= 0 ? 0 : Math.Clamp(1.0 - (double)means / reserve, 0, 1);
        double pantry = home is null ? 0 : PantryShortfall(world, home);
        return rules.SiteOfferMax * Math.Max(money, pantry) * share;
    }

    /// <summary>A oferta da obra em que o NPC trabalha, ou a melhor das que contratam na cidade dele, para o contexto da decisão.</summary>
    internal static double OfferFor(WorldState world, Npc npc)
    {
        if (npc.Employer is not null || !AnySite(world)) return 0;
        if (WorkOf(world, npc) is { } mine) return OfferValue(world, npc, mine);
        double best = 0;
        foreach (var site in Sites(world))
            if (site.Crew.Count < world.StreetRules.CrewMax) best = Math.Max(best, OfferValue(world, npc, site));
        return best;
    }

    private static double PantryShortfall(WorldState world, LivingWorld.Domain.Population.Family.Household home)
    {
        var economy = world.EconomyRules;
        if (!economy.Enabled || home.Members.Count == 0) return 0;
        long scarcest = Math.Min(home.Stock.GetValueOrDefault(new ResourceType(economy.FoodResourceId)),
            home.Stock.GetValueOrDefault(new ResourceType(economy.WaterResourceId)));
        double days = (double)scarcest / home.Members.Count;
        return Math.Clamp(1.0 - days / Math.Max(1, world.StreetRules.SiteReserveDays / 3.0), 0, 1);
    }

    internal static void Staff(WorldState world, TickContext ctx, PavingWork work) => Staff(world, ctx, new WorkSite(work, null, null));

    /// <summary>Diária: a turma de uma obra. Sai quem morreu, arrumou emprego, mudou de cidade ou já não precisa (oferta abaixo da
    /// metade do mínimo); entra, até o teto, quem quer (oferta a partir de <see cref="StreetRules.SiteJoinMin"/>), quem mais precisa
    /// primeiro (empate por id). Quem já está noutra obra não entra nesta.</summary>
    internal static void Staff(WorldState world, TickContext ctx, WorkSite site)
    {
        var rules = world.StreetRules;
        var crew = site.Crew;
        for (int i = crew.Count - 1; i >= 0; i--)
        {
            var npc = world.FindNpc(new NpcId(crew[i]));
            if (npc is { IsAlive: true } && OfferValue(world, npc, site) >= rules.SiteJoinMin / 2.0) continue;
            string why = npc is not { IsAlive: true } ? "morto" : npc.Employer is not null ? "emprego" : "saiu";
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{crew[i]}|{site.Label}|{why}", sourceSystem: SourceSystem);
            crew.RemoveAt(i);
        }
        if (crew.Count >= rules.CrewMax) return;
        var cityId = new CityId(site.CityId);
        List<(double Value, Npc Npc)>? willing = null;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            if (npc.City != cityId || npc.Employer is not null || WorkOf(world, npc) is not null) continue;
            double value = OfferValue(world, npc, site);
            if (value >= rules.SiteJoinMin) (willing ??= []).Add((value, npc));
        }
        if (willing is null) return;
        willing.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : a.Npc.Id.Value.CompareTo(b.Npc.Id.Value));
        foreach (var (value, npc) in willing)
        {
            if (crew.Count >= rules.CrewMax) break;
            crew.Add(npc.Id.Value);
            ctx.LogEvent(WorldEventKind.SiteWorkJoined, $"{npc.Id.Value}|{site.Label}|{(long)Math.Round(value * 100)}", sourceSystem: SourceSystem);
        }
    }

    /// <summary>A jornada do diarista acabou ou foi interrompida: as horas em que ele esteve NA obra trabalhando contam para ela e
    /// são pagas agora (por hora). Quem paga não tem: o diarista sai sem receber e a hora não conta (ninguém trabalha de graça).
    /// No trecho, as horas descontam a obra; no prédio, ficam guardadas para o passo da fila (<c>ConstructionSystem</c>).</summary>
    internal static void Clock(WorldState world, TickContext ctx, Npc npc, long startedAt, long now)
    {
        if (WorkOf(world, npc) is not { } site) return;
        if (npc.CurrentLocation != SiteOf(world, site)) return; // não estava lá: a ida não é jornada
        long hours = Math.Min(now - startedAt, world.StreetRules.CrewHoursPerDay);
        if (hours <= 0) return;
        if (world.FindActiveCity(new CityId(site.CityId)) is not { } city) return;
        long due = hours * HourWage(world);
        if (!PavingDecision.PayFrom(world, ctx, city, site.Paving?.Payers ?? [], due))
        {
            site.Crew.Remove(npc.Id.Value);
            ctx.LogEvent(WorldEventKind.SiteWorkLeft, $"{npc.Id.Value}|{site.Label}|sem-pagamento", sourceSystem: SourceSystem);
            world.CanonicalHashCache.MarkPropertyDirty(site.Paving is null ? nameof(WorldState.Cities) : nameof(WorldState.Ground));
            return;
        }
        npc.CreditWallet(new Money(due));
        if (site.Paving is { } work)
        {
            work.Paid += due;
            work.HoursWorked += hours;
            work.HoursLeft -= hours;
            work.LastProgressTick = now;
            if (work.HoursLeft <= 0) PavingWorksSystem.Finish(world, ctx, work);
            world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Ground));
            return;
        }
        // a força de quem trabalha rende mais horas de obra (o poder modula a base, AD-084), como na fila sem turma
        long done = (long)Math.Round(hours * LivingWorld.Simulation.Extraordinary.Mechanics.AttributeMechanic.StrengthMultiplier(world, npc), MidpointRounding.AwayFromZero);
        site.Project!.HoursBanked += done;
        site.Project.HoursWorked += hours;
        site.Project.Paid += due;
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Cities));
    }
}
