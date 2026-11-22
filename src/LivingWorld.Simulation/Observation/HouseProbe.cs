using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>AD-191 (2ª rodada): a sonda de casa para qualquer cenário (o <c>person-trace</c> só roda a vila de código). Uma linha por
/// casa com a despensa, o preço e o estoque de comida do mercado mais perto e, por morador, idade, carteira, ação, motivo da viagem,
/// fome, sede, intento e, para quem decide, a conta da decisão. Só lê o mundo.</summary>
public static class HouseProbe
{
    public static IEnumerable<string> Lines(WorldState world, IReadOnlyCollection<long> households, bool explain)
    {
        var inv = CultureInfo.InvariantCulture;
        long now = world.CurrentDate.TotalHours;
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        string stamp = string.Create(inv, $"y{now / world.Calendar.HoursPerYear} d{now / 24 % 360} h{now % 24}");
        foreach (var id in households.OrderBy(h => h))
        {
            if (world.FindHousehold(new HouseholdId(id)) is not { } home) { yield return $"{stamp} CASA {id} -"; continue; }
            var market = LivingWorld.Simulation.Economy.Market.MarketIndex.BuildForTick(world).NearestSelling(home.Location, food, water);
            string m = market is null ? "-" : string.Create(inv,
                $"{market.Id.Value}@{market.Location.X}:{market.Location.Y} p={market.Prices.GetValueOrDefault(food)} est={market.Stock.GetValueOrDefault(food)} rac={(market.RationedSale ? 1 : 0)} h={LivingWorld.Simulation.Cities.Streets.StreetRoutes.Ticks(world, home.Location, market.Location)}");
            yield return string.Create(inv,
                $"{stamp} CASA {id} at={home.Location.X}:{home.Location.Y} comida={home.Stock.GetValueOrDefault(food)} agua={home.Stock.GetValueOrDefault(water)} chefe={home.Head.Value} mercado={m} retry={home.FoodStallRetryTick - now}");
            foreach (var mid in home.Members.OrderBy(x => x.Value))
            {
                if (world.FindNpc(mid) is not { IsAlive: true } n) continue;
                double age = (now - n.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
                string line = string.Create(inv,
                    $"{stamp}   NPC {n.Id.Value} idade={age:F1} cart={n.Wallet.Amount} emp={n.Employer?.Value.ToString(inv) ?? "-"} acao={n.CurrentAction} desde={now - n.ActionStartedAtTick} motivo={n.TravelPurpose?.ToString() ?? "-"} fome={n.HungerAt(now)} sede={n.ThirstAt(now)} sono={n.SleepAt(now)} intento={n.CurrentIntent?.ToString() ?? "-"}/{n.IntentStatus?.ToString() ?? "-"} em={n.CurrentLocation.X}:{n.CurrentLocation.Y} casa={(n.CurrentLocation == home.Location ? 1 : 0)}");
                if (explain && age >= 8) line += " | " + LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.ExplainDecision(world, n);
                yield return line;
            }
        }
    }
}
