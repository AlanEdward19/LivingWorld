using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Sonda (AD-147, item 47): roda hora a hora e, a cada morte de sede do cenário padrão, imprime as últimas 48 horas do
/// morto (ação, motivo, sede, fome, água em casa, carteira, intenção, em casa). Só lê o mundo.</summary>
public static class ThirstTraceCommand
{
    private sealed class Sink(WorldState world, Dictionary<long, Queue<string>> history, List<string> lines, ulong seed) : IWorldEventSink
    {
        public void Record(WorldEvent evt)
        {
            if (evt.Kind != WorldEventKind.Dehydration || evt.SourceSystem != "NpcDeath") return;
            long id = long.Parse(evt.Payload, CultureInfo.InvariantCulture);
            lines.Add($"{seed} death npc={id} tick={world.CurrentDate.TotalHours}");
            if (history.TryGetValue(id, out var q)) foreach (var l in q) lines.Add($"{seed}   {l}");
        }
    }

    public static int Run(ulong seed, int years)
    {
        var lines = new List<string>();
        var history = new Dictionary<long, Queue<string>>();
        var (world, baseClock) = ScenarioRunner.Create(seed);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new Sink(world, history, lines, seed));
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        long hours = (long)years * world.Calendar.HoursPerYear;
        for (long h = 0; h < hours && world.AliveNpcIndex.Alive.Count > 0; h++)
        {
            clock.Run(world, 1);
            long now = world.CurrentDate.TotalHours;
            var byId = world.Households.ToDictionary(x => x.Id);
            foreach (var npc in world.AliveNpcIndex.Alive)
            {
                if (npc.ThirstAt(now) > 40) { history.Remove(npc.Id.Value); continue; }
                if (!history.TryGetValue(npc.Id.Value, out var q)) history[npc.Id.Value] = q = new Queue<string>();
                var home = npc.Household is { } hid ? byId.GetValueOrDefault(hid) : null;
                q.Enqueue(string.Create(CultureInfo.InvariantCulture,
                    $"t={now} act={npc.CurrentAction} since={npc.ActionStartedAtTick} purpose={npc.TravelPurpose?.ToString() ?? "-"} thirst={npc.ThirstAt(now)} hunger={npc.HungerAt(now)} water={home?.Stock.GetValueOrDefault(water) ?? -1} food={home?.Stock.GetValueOrDefault(food) ?? -1} wallet={npc.Wallet.Amount} intent={npc.CurrentIntent?.ToString() ?? "-"}/{npc.IntentStatus?.ToString() ?? "-"} atHome={(home is not null && home.Location == npc.CurrentLocation ? 1 : 0)} loc={npc.CurrentLocation.X},{npc.CurrentLocation.Y} home={home?.Location.X},{home?.Location.Y} age={npc.AgeYears(world.CurrentDate)} members={home?.Members.Count ?? 0}"));
                if (npc.CurrentAction == LivingWorld.Domain.Behavior.ActionType.Buy)
                {
                    var here = world.Workplaces.FirstOrDefault(w => w.Location == npc.CurrentLocation && w.Prices.Count > 0);
                    if (here is not null)
                        q.Enqueue(string.Create(CultureInfo.InvariantCulture,
                            $"   market {here.Id.Value} type={here.LocationType.Id} stockF={here.Stock.GetValueOrDefault(food)} stockW={here.Stock.GetValueOrDefault(water)} priceF={here.Prices.GetValueOrDefault(food)} priceW={here.Prices.GetValueOrDefault(water)}"));
                }
                while (q.Count > 48) q.Dequeue();
            }
            if (lines.Count > 4000) break;
        }
        foreach (var l in lines) Console.WriteLine(l);
        return 0;
    }
}
