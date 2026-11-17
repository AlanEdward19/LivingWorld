using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Sonda de uma pessoa (AD-182, 08c item 149): <c>person-trace &lt;seed&gt; &lt;npc&gt; &lt;anos&gt;</c>. Uma linha por dia a partir dos
/// 13 anos (carteira, emprego, casa, despensa, fome, ação), os eventos que citam o id dela e, nos 20 dias antes da morte, uma
/// linha por hora. Só lê o mundo; mesmas chaves de ambiente dos relatórios (<c>LW_PROVISION</c>, <c>LW_PRESSURE</c>).</summary>
public static class PersonTrace
{
    private sealed class Sink(string id, List<string> lines, Func<string> stamp) : IWorldEventSink
    {
        public void Record(WorldEvent evt)
        {
            foreach (var tok in evt.Payload.Split('|', '-', ':', ','))
                if (tok == id) { lines.Add($"{stamp()} EV {evt.Kind} {evt.SourceSystem} {evt.Payload}"); return; }
        }
    }

    public static int Run(WorldState world, WorldClock baseClock, long npcId, int years)
    {
        var daily = new List<string>();
        var hourly = new Queue<string>();
        var inv = CultureInfo.InvariantCulture;
        string Stamp() => string.Create(inv, $"y{world.CurrentDate.TotalHours / world.Calendar.HoursPerYear} d{world.CurrentDate.TotalHours / 24 % 360} h{world.CurrentDate.TotalHours % 24}");
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, new Sink(npcId.ToString(inv), daily, Stamp));
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var id = new NpcId(npcId);
        long end = (long)years * world.Calendar.HoursPerYear;
        long explainFrom = long.TryParse(Environment.GetEnvironmentVariable("LW_TRACE_DECISION_FROM"), out long from) ? from : long.MaxValue;
        while (world.CurrentDate.TotalHours < end)
        {
            clock.Run(world, 1);
            var npc = world.FindNpc(id);
            if (npc is null) continue;
            long now = world.CurrentDate.TotalHours;
            double age = (now - npc.BirthDate.TotalHours) / (double)world.Calendar.HoursPerYear;
            if (age < 13) continue;
            var home = npc.Household is { } hid ? world.FindHousehold(hid) : null;
            int alive = home?.Members.Count(m => world.FindNpc(m) is { IsAlive: true }) ?? 0;
            string line = string.Create(inv,
                $"{Stamp()} age={age:F1} wal={npc.Wallet.Amount} emp={npc.Employer?.Value.ToString(inv) ?? "-"} hh={npc.Household?.Value.ToString(inv) ?? "-"}/{alive} " +
                $"stock={home?.Stock.GetValueOrDefault(food) ?? -1} carry={world.Invention?.Carried.Where(c => c.NpcId == npcId && c.ResourceId == food.Id).Sum(c => c.Quantity) ?? 0} hun={npc.HungerAt(now)} thi={npc.ThirstAt(now)} " +
                $"act={npc.CurrentAction} home={(home is not null && home.Location == npc.CurrentLocation ? 1 : 0)} at={npc.CurrentLocation} purpose={npc.TravelPurpose?.ToString() ?? "-"} intent={npc.CurrentIntent?.ToString() ?? "-"}/{npc.IntentStatus?.ToString() ?? "-"}");
            if (!npc.IsAlive) { daily.Add($"MORTE {line}"); break; }
            // AD-182: a partir da hora LW_TRACE_DECISION_FROM, a conta da decisão de cada hora
            if (now >= explainFrom) line += " | " + LivingWorld.Simulation.Behavior.Decision.BehaviorDecisionSystem.ExplainDecision(world, npc);
            hourly.Enqueue(line);
            if (hourly.Count > 480) hourly.Dequeue();
            if (now % 24 == 0) daily.Add(line);
        }
        foreach (var l in daily) Console.WriteLine(l);
        Console.WriteLine("--- ultimas horas ---");
        foreach (var l in hourly) Console.WriteLine(l);
        return 0;
    }
}
