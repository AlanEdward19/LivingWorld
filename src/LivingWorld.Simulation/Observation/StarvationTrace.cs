using System.Globalization;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Uma linha por morte de fome com a situação do morto e da casa no instante da morte (AD-140). Só lê o mundo.</summary>
public sealed class StarvationTrace(WorldState world, ulong seed, List<string> lines) : IWorldEventSink
{
    public void Record(WorldEvent evt)
    {
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
        lock (lines)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"{seed} {evt.Kind.ToString()[..5]} {now / world.Calendar.HoursPerYear} {world.CurrentDate.Month} age={age:F1} sex={npc.Sex} emp={(npc.Employer is null ? 0 : 1)} " +
                $"hh={(npc.Household is null ? "none" : size.ToString(CultureInfo.InvariantCulture))} grown={grown} hhEmp={employed} stock={stock} wallets={wallets} " +
                $"mother={(motherAlive ? motherHome ? "home" : "away" : "dead")} market={market} treasury={treasury} sick={sick} alive={world.AliveNpcIndex.Alive.Count} act={npc.CurrentAction} dev={npc.Development?.ToString() ?? "-"}"));
    }
}
