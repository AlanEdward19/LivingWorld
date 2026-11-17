using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Sonda da quebra de colheita (AD-166, 08c item 148): <c>famine-trace &lt;seed&gt; &lt;anos&gt;</c>, uma linha por mês do
/// cenário padrão com as casas carentes (despensa abaixo de um dia por morador), quantas delas a ajuda deixa de fora por terem
/// moeda (4 dias de compra pelo preço-piso), a moeda e a comida das casas (despensa e o que os moradores levam), a venda de comida,
/// furtos de comida, ajudas em moeda e em comida, esmolas e banidos. Só lê o mundo. <c>LW_PROVISION=-carry</c> desliga a carga.</summary>
public static class FamineTraceCommand
{
    private sealed class Sink : IWorldEventSink
    {
        public long Thefts, AidMoney, AidFood, AidFoodUnits, Alms, Banished, Starved;

        public void Record(WorldEvent evt)
        {
            switch (evt.Kind)
            {
                case WorldEventKind.TheftCommitted when evt.Payload.Split('|').ElementAtOrDefault(4) == "comida": Thefts++; break;
                case WorldEventKind.AidGiven when evt.Payload.StartsWith("city-", StringComparison.Ordinal): Alms++; break;
                case WorldEventKind.AidGiven when evt.Payload.EndsWith("|food", StringComparison.Ordinal):
                    AidFood++;
                    AidFoodUnits += long.Parse(evt.Payload.Split('|')[2], CultureInfo.InvariantCulture);
                    break;
                case WorldEventKind.AidGiven: AidMoney++; break;
                case WorldEventKind.Banished when evt.Payload.Split('|').ElementAtOrDefault(3) != "fuga": Banished++; break;
                case WorldEventKind.Starvation: Starved++; break;
            }
        }
    }

    public static int Run(ulong seed, int years)
    {
        var (world, baseClock) = ScenarioRunner.Create(seed, familyRules: PressureSwitch.FamilyRules(), needsRules: ProvisionSwitch.NeedsRules(), inventionRules: ProvisionSwitch.InventionRules(), agricultureRules: AgricultureSwitch.AgricultureRules(), ontogenyRules: ExperimentSwitch.OntogenyRules());
        var sink = new Sink();
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var inv = CultureInfo.InvariantCulture;
        Console.WriteLine("ano mes vivos casas carentes carentes_com_moeda moeda_casas moeda_carentes despensa levado venda preco furtos ajuda_moeda ajuda_comida(unid) esmolas banidos fome celeiro semente");
        for (int m = 0; m < years * 12; m++)
        {
            clock.Run(world, world.Calendar.HoursPerMonth);
            long unit = Math.Max(1, world.EconomyRules.PriceFloor.GetValueOrDefault(world.EconomyRules.FoodResourceId, 1));
            var byId = world.AliveNpcIndex.Alive.ToDictionary(n => n.Id);
            int houses = 0, needy = 0, needyWithMoney = 0;
            long money = 0, needyMoney = 0, pantry = 0;
            foreach (var h in world.Households)
            {
                var alive = h.Members.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                if (alive.Count == 0) continue;
                houses++;
                long wallets = alive.Sum(n => n.Wallet.Amount);
                money += wallets;
                pantry += h.Stock.GetValueOrDefault(food);
                bool scarce = h.Stock.GetValueOrDefault(food) < alive.Count || h.Stock.GetValueOrDefault(water) < alive.Count;
                if (!scarce) continue;
                needy++;
                needyMoney += wallets;
                if (wallets >= alive.Count * 2 * unit * 4) needyWithMoney++;
            }
            long carried = (world.Invention?.CarriedTotal(food.Id) ?? 0) + (world.Invention?.GroundTotal(food.Id) ?? 0); // AD-168: e o espólio no chão
            long barn = 0, seedStock = 0; // AD-182: o celeiro e a semente das fazendas
            foreach (var w in world.Workplaces)
                if (w.LocationType.Id == LivingWorld.Simulation.Economy.Production.CropSystem.DefaultFarmLocationTypeId && world.FindCropAt(w.Location) is { } crop)
                { barn += crop.Barn; seedStock += crop.SeedReserve; }
            long sale = 0, price = 0;
            foreach (var w in world.Workplaces)
                if (w.Prices.TryGetValue(food, out var p) && p > 0) { sale += w.Stock.GetValueOrDefault(food); price = Math.Max(price, p); }
            Console.WriteLine(string.Create(inv,
                $"{m / 12} {m % 12} {world.AliveNpcIndex.Alive.Count} {houses} {needy} {needyWithMoney} {money} {needyMoney} {pantry} {carried} {sale} {price} " +
                $"{sink.Thefts} {sink.AidMoney} {sink.AidFood}({sink.AidFoodUnits}) {sink.Alms} {sink.Banished} {sink.Starved} {barn} {seedStock}"));
            sink.Thefts = sink.AidMoney = sink.AidFood = sink.AidFoodUnits = sink.Alms = sink.Banished = sink.Starved = 0;
            if (world.AliveNpcIndex.Alive.Count == 0) break;
        }
        return 0;
    }
}
