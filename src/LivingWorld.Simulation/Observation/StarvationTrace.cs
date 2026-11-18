using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Uma linha por morte de fome com a situação do morto e da casa no instante da morte (AD-140). Só lê o mundo.</summary>
public sealed class StarvationTrace(WorldState world, ulong seed, List<string> lines) : IWorldEventSink
{
    /// <summary>AD-182: a herança é repartida antes do evento da morte; a carteira do morto já está zerada quando a linha é escrita
    /// (o "carteira zerada" do 08c item 149 era isso). Guarda a moeda que a herança levou.</summary>
    private readonly Dictionary<string, string> _estate = [];

    public void Record(WorldEvent evt)
    {
        if (evt.Kind == WorldEventKind.EstateSettled && evt.Payload.Split('|') is { Length: >= 3 } parts) { _estate[parts[0]] = parts[2]; return; }
        if (evt.Kind is not (WorldEventKind.Starvation or WorldEventKind.Dehydration) || evt.SourceSystem != "NpcDeath") return;
        var npc = world.FindNpc(new NpcId(long.Parse(evt.Payload, CultureInfo.InvariantCulture)));
        if (npc is null) return;
        var food = new ResourceType(evt.Kind == WorldEventKind.Dehydration ? world.EconomyRules.WaterResourceId : world.EconomyRules.FoodResourceId);
        long now = world.CurrentDate.TotalHours;
        double age = (now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
        int size = 0, grown = 0, employed = 0; long stock = -1, wallets = 0;
        bool motherHome = false, motherAlive = npc.MotherId is { } m && world.FindNpc(m) is { IsAlive: true };
        if (npc.Household is { } hid && world.FindHousehold(hid) is { } home)
        {
            stock = home.Stock.GetValueOrDefault(food);
            foreach (var id in home.Members)
            {
                if (id == npc.Id || world.FindNpc(id) is not { IsAlive: true } o) continue;
                size++; wallets += o.Wallet.Amount;
                if (o.AgeYears(world.CurrentDate) >= 15) grown++;
                if (o.Employer is not null) employed++;
                if (npc.MotherId == id) motherHome = true;
            }
        }
        long market = 0;
        foreach (var w in world.Workplaces)
            if (w.Prices.Count > 0 && w.City == npc.City) market += w.Stock.GetValueOrDefault(food);
        long treasury = world.FindCity(npc.City)?.Treasury.Amount ?? -1;
        bool sick = LivingWorld.Simulation.Health.SickBehavior.IsBedridden(world, npc.Id);
        // AD-184: onde a casa mora e quanto custa chegar à água e ao mercado; se ela está em obra ou de favor; se largou o ofício
        string place = "-";
        if (npc.Household is { } ph && world.FindHousehold(ph) is { } ph2)
        {
            var ground = world.Ground;
            bool favor = ground?.Lodgers.Contains(ph2.Id.Value) == true, building = ground?.Builds.Any(b => b.Household == ph2.Id.Value) == true;
            long water = LivingWorld.Simulation.Economy.Supply.NaturalWaterSystem.WalkToWater(world, ph2.Location, 30);
            double market2 = double.MaxValue;
            foreach (var w in world.Workplaces)
                if (w.City == npc.City && world.EconomyCatalog.MarketLocationTypeIds.Contains(w.LocationType.Id))
                    market2 = Math.Min(market2, world.StreetsOn ? LivingWorld.Simulation.Cities.Streets.StreetRoutes.CostOf(world, ph2.Location, w.Location).Cost
                        : LivingWorld.Domain.Behavior.TravelResolution.TicksBetween(world.Map, ph2.Location, w.Location));
            place = string.Create(CultureInfo.InvariantCulture,
                $"dw={(ph2.Dwelling is null ? "none" : favor ? "favor" : "own")} build={(building ? 1 : 0)} hWater={water} hMarket={(market2 == double.MaxValue ? -1 : market2):F1} quit={(npc.LastJobChangeTick > 0 && npc.Employer is null ? (now - npc.LastJobChangeTick) / 24 : -1)} crew={(LivingWorld.Simulation.Cities.Streets.SiteWork.WorkOf(world, npc) is null ? 0 : 1)} help={(LivingWorld.Simulation.Cities.Streets.SiteWork.IsHelper(world, npc) ? 1 : 0)}");
        }
        lock (lines)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{seed} {evt.Kind.ToString()[..5]} {now / world.Calendar.HoursPerYear} {world.CurrentDate.Month} id={npc.Id.Value} age={age:F1} sex={npc.Sex} emp={(npc.Employer is null ? 0 : 1)} " +
                $"hh={(npc.Household is null ? "none" : size.ToString(CultureInfo.InvariantCulture))} grown={grown} hhEmp={employed} stock={stock} wallets={wallets} " +
                $"mother={(motherAlive ? motherHome ? "home" : "away" : "dead")} market={market} treasury={treasury} sick={sick} alive={world.AliveNpcIndex.Alive.Count} act={npc.CurrentAction} for={now - npc.ActionStartedAtTick} purpose={npc.TravelPurpose?.ToString() ?? "-"} intent={npc.CurrentIntent?.ToString() ?? "-"}/{npc.IntentStatus?.ToString() ?? "-"} wallet={npc.Wallet.Amount} heranca={_estate.GetValueOrDefault(evt.Payload, "-")} atHome={(npc.Household is { } h2 && world.FindHousehold(h2) is { } hh2 ? (hh2.Location == npc.CurrentLocation ? 1 : 0) : -1)} dev={npc.Development?.ToString() ?? "-"} {place}"));
    }
}
