using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Streets;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Shared.Cities;

/// <summary>AD-179: o mundo anda? Amostra de hora em hora (só lê o mundo): viagens por motivo (pela célula de chegada),
/// passos de ambiente, horas por ação, quem está em rota de ida (5–8 h) e de volta (14–18 h), fome e sede crítica, mortes.
/// <see cref="Snapshot"/> devolve a linha do período com distâncias, emprego e onde estão trabalho, mercado e água.</summary>
public sealed class MenuWorldProbe(WorldState world) : IWorldEventSink
{
    private readonly Dictionary<long, (CellCoord Loc, ActionType? Act, ActionType? Purpose)> _last = [];
    public SortedDictionary<string, long> Trips { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, long> ActionHours { get; } = new(StringComparer.Ordinal);
    public long PersonHours, RouteMorning, RouteEvening, RouteHours, HungerCrit, ThirstCrit, Deaths, Starved, Dehydrated;
    public long RoutesWithCells;

    /// <summary>Eventos da obra e da técnica (AD-179): entrou na turma, saiu, obra aberta, obra pronta, tecnologia descoberta.</summary>
    public SortedDictionary<string, long> Works { get; } = new(StringComparer.Ordinal);

    public void Record(WorldEvent evt)
    {
        if (evt.Kind is WorldEventKind.SiteWorkJoined or WorldEventKind.SiteWorkLeft or WorldEventKind.StreetWorkStarted
            or WorldEventKind.StreetWorkDone or WorldEventKind.TechnologyDiscovered)
        {
            string k = evt.Kind == WorldEventKind.TechnologyDiscovered ? "tec"
                : evt.Kind == WorldEventKind.SiteWorkLeft ? "SiteWorkLeft:" + evt.Payload?.Split('|').LastOrDefault()
                : evt.Kind.ToString();
            Works[k] = Works.GetValueOrDefault(k) + 1;
        }
        if (evt.SourceSystem != YearEventTally.DeathSource) return;
        if (evt.Kind is WorldEventKind.Death or WorldEventKind.Starvation or WorldEventKind.Dehydration or WorldEventKind.Exhaustion
            or WorldEventKind.Disease or WorldEventKind.MaternalDeath or WorldEventKind.Accident or WorldEventKind.OldAge
            or WorldEventKind.Homicide or WorldEventKind.Executed) Deaths++;
        if (evt.Kind == WorldEventKind.Starvation) Starved++;
        if (evt.Kind == WorldEventKind.Dehydration) Dehydrated++;
    }

    public void SampleHour()
    {
        long now = world.CurrentDate.TotalHours;
        int hour = (int)(now % world.Calendar.HoursPerDay);
        MarketIndex? markets = null;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            PersonHours++;
            string act = npc.CurrentAction?.ToString() ?? "-";
            ActionHours[act] = ActionHours.GetValueOrDefault(act) + 1;
            if (100 - npc.HungerAt(now) > NeedDistressTally.Critical) HungerCrit++;
            if (100 - npc.ThirstAt(now) > NeedDistressTally.Critical) ThirstCrit++;
            if (npc.CurrentAction == ActionType.Travel)
            {
                RouteHours++;
                markets ??= MarketIndex.BuildForTick(world);
                if (BehaviorDecisionSystem.TravelDestinationFor(world, npc, markets) is { } to && to != npc.CurrentLocation
                    && StreetRoutes.PathOf(world, npc.CurrentLocation, to).Count > 1)
                {
                    RoutesWithCells++;
                    if (hour is >= 5 and <= 8) RouteMorning++;
                    if (hour is >= 14 and <= 18) RouteEvening++;
                }
            }
            if (_last.TryGetValue(npc.Id.Value, out var before) && before.Loc != npc.CurrentLocation)
            {
                string kind = before.Act == ActionType.Travel ? KindOf(npc, npc.CurrentLocation, before.Purpose) : "passo-" + (before.Act?.ToString() ?? "-");
                Trips[kind] = Trips.GetValueOrDefault(kind) + 1;
            }
            _last[npc.Id.Value] = (npc.CurrentLocation, npc.CurrentAction, npc.TravelPurpose);
        }
    }

    private string KindOf(LivingWorld.Domain.Population.Npc npc, CellCoord at, ActionType? purpose)
    {
        if (npc.Household is { } h && world.FindHousehold(h) is { } home && home.Location == at) return "casa";
        if (npc.Employer is { } e && world.FindWorkplace(e) is { } place && place.Location == at) return "trabalho";
        if (world.Workplaces.Any(w => w.Location == at)) return purpose == ActionType.Work ? "trabalho-outro" : "mercado";
        if (world.Map.TryGetCell(at, out var cell) && cell.HasWater) return "agua";
        if (world.Households.Any(o => o.Location == at)) return "casa-alheia";
        return "outro-" + (purpose?.ToString() ?? "-");
    }

    /// <summary>A linha do período agora: contas da amostra desde o começo, por pessoa-mês, mais o retrato do mundo.</summary>
    public string Snapshot(string label)
    {
        var inv = CultureInfo.InvariantCulture;
        double pm = PersonHours / (double)(world.Calendar.HoursPerDay * world.Calendar.DaysPerMonth);
        string Per(long n) => pm <= 0 ? "0" : (n / pm).ToString("F2", inv);
        var alive = world.AliveNpcIndex.Alive;
        int adults = alive.Count(n => n.AgeYears(world.CurrentDate) is >= 16 and <= 64);
        int employed = alive.Count(n => n.Employer is not null);
        var marketTypes = world.EconomyCatalog.MarketLocationTypeIds;
        var places = world.Workplaces.Where(w => !w.Closed).ToList();
        var markets = places.Where(w => marketTypes.Contains(w.LocationType.Id)).ToList();
        int waterCells = 0;
        for (int y = 0; y < world.Map.Height; y++)
            for (int x = 0; x < world.Map.Width; x++)
                if (world.Map.TryGetCell(new CellCoord(x, y), out var c) && c.HasWater) waterCells++;
        double work = 0; int workN = 0, sameCell = 0;
        foreach (var npc in alive)
        {
            if (npc.Household is not { } h || world.FindHousehold(h) is not { } home || npc.Employer is not { } e
                || world.FindWorkplace(e) is not { } place) continue;
            if (home.Location == place.Location) sameCell++;
            work += world.StreetsOn ? StreetRoutes.CostOf(world, home.Location, place.Location).Cost
                : LivingWorld.Domain.Behavior.TravelResolution.TicksBetween(world.Map, home.Location, place.Location);
            workN++;
        }
        double water = 0, market = 0; int waterN = 0, waterNear = 0, marketN = 0, homes = 0;
        foreach (var home in world.Households)
        {
            if (home.IsEmpty) continue;
            homes++;
            long t = LivingWorld.Simulation.Economy.Supply.NaturalWaterSystem.WalkToWater(world, home.Location, 30);
            if (t != long.MaxValue) { water += t; waterN++; if (t <= world.NaturalWaterRules.MaxWalkTicks) waterNear++; }
            if (markets.Count > 0)
            {
                market += markets.Min(m => world.StreetsOn ? StreetRoutes.CostOf(world, home.Location, m.Location).Cost
                    : LivingWorld.Domain.Behavior.TravelResolution.TicksBetween(world.Map, home.Location, m.Location));
                marketN++;
            }
        }
        string trips = string.Join(' ', Trips.Select(kv => $"{kv.Key}={Per(kv.Value)}"));
        string acts = string.Join(' ', ActionHours.Select(kv => string.Create(inv, $"{kv.Key}={kv.Value / (double)Math.Max(1, PersonHours):F2}")));
        string Where(IEnumerable<Workplace> ws) => string.Join(',', ws.Take(8).Select(w => $"{w.LocationType.Id}@{w.Location.X}:{w.Location.Y}"));
        return string.Create(inv,
            $"{label} pop={alive.Count} adultos={adults} empregados={employed} fracEmpregada={(adults == 0 ? 0 : employed / (double)adults):F2} " +
            $"casas={homes} locaisTrabalho={places.Count} [{Where(places)}] mercados={markets.Count} celulasAgua={waterCells} " +
            $"casasComAgua1h={waterNear}/{homes} economia={(world.EconomyRules.Enabled ? 1 : 0)} " +
            $"hCasaTrabalho={(workN == 0 ? 0 : work / workN):F2} casaTrabalhoMesmaCelula={sameCell}/{workN} hCasaAgua={(waterN == 0 ? 0 : water / waterN):F2} " +
            $"hCasaMercado={(marketN == 0 ? 0 : market / marketN):F2} | viagensPorPessoaMes {trips} | horasEmRota={Per(RouteHours)} rotaIda={RouteMorning} rotaVolta={RouteEvening} " +
            $"| acoes {acts} | fomeCritH={Per(HungerCrit)} sedeCritH={Per(ThirstCrit)} mortes={Deaths} fome={Starved} sede={Dehydrated} " +
            $"| obra[{string.Join(' ', Works.Select(kv => kv.Key + "=" + kv.Value))}] tecs={world.Technologies.Count} abertas[{string.Join(' ', (world.Ground?.Works ?? []).Select(w => $"{w.Id}:falta{w.HoursLeft}h/feito{w.HoursWorked}h/turma{w.Crew.Count}/pagam{w.Payers.Count}"))}] | {StreetDiagnostics.Line(world)}");
    }
}
