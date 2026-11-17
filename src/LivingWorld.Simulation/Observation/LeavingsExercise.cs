using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Observation;

/// <summary>Exercício do espólio (AD-182, 08c item 155): no cenário de referência quase ninguém morre longe levando carga, então os
/// números da regra (busca até 24 h, furto 250‰, 0,15 por testemunha, idade 12) não se exerciam. A sonda provoca, a cada
/// <c>everyHours</c>, a morte por acidente de um viajante longe de casa que leva carga (o de menor id entre os que o hash da hora
/// escolhe) e mede o destino de cada espólio pela distância de casa: buscado pela casa, recolhido de passagem, achado, furtado,
/// estragado ou ainda no chão. É um experimento: muda o mundo da rodada; nenhum sistema o chama.</summary>
public static class LeavingsExercise
{
    private sealed class Sink : IWorldEventSink
    {
        public readonly Dictionary<long, (long Tick, long Household, int X, int Y)> Left = [];
        public readonly Dictionary<long, (long Tick, string How)> Collected = [];
        public long GroundThefts;

        public void Record(WorldEvent evt)
        {
            if (evt.Kind == WorldEventKind.LeavingsLeft)
            {
                var p = evt.Payload.Split('|');
                Left[long.Parse(p[0], CultureInfo.InvariantCulture)] = (evt.Tick, long.Parse(p[2], CultureInfo.InvariantCulture),
                    int.Parse(p[4], CultureInfo.InvariantCulture), int.Parse(p[5], CultureInfo.InvariantCulture));
            }
            else if (evt.Kind == WorldEventKind.LeavingsCollected)
            {
                var p = evt.Payload.Split('|');
                long id = long.Parse(p[0], CultureInfo.InvariantCulture);
                if (!Collected.ContainsKey(id)) Collected[id] = (evt.Tick, p[5]);
            }
            else if (evt.Kind == WorldEventKind.TheftCommitted && evt.Payload.Contains("|espolio|", StringComparison.Ordinal)) GroundThefts++;
        }
    }

    public static IReadOnlyList<string> Run(WorldState world, WorldClock baseClock, int years, int everyHours)
    {
        var sink = new Sink();
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        long end = (long)years * world.Calendar.HoursPerYear;
        var distance = new Dictionary<long, long>();
        int killed = 0;
        while (world.CurrentDate.TotalHours < end && world.AliveNpcIndex.Alive.Count > 0)
        {
            clock.Run(world, everyHours);
            if (!world.InventionOn) continue;
            var ledger = world.Invention!;
            var travelers = new List<LivingWorld.Domain.Population.Npc>();
            foreach (var npc in world.AliveNpcIndex.Alive)
                if (npc.CurrentAction == ActionType.Travel && npc.Household is { } h && world.FindHousehold(h) is { } home
                    && ledger.CarriesAnything(npc.Id.Value) && LivingWorld.Simulation.Invention.PersonalCarry.PantryOutOfReach(world, npc, home))
                    travelers.Add(npc);
            if (travelers.Count == 0) continue;
            ulong mix = unchecked((ulong)world.CurrentDate.TotalHours * 0x9E3779B97F4A7C15UL);
            var victim = travelers[(int)(mix % (ulong)travelers.Count)];
            var victimHome = world.FindHousehold(victim.Household!.Value)!;
            long ticks = LivingWorld.Simulation.Emergence.Mobility.HouseholdTicks(world, victimHome, victimHome.Location, victim.CurrentLocation);
            LivingWorld.Simulation.Population.Lifecycle.NpcDeath.Apply(world, ctx, victim, WorldEventKind.Accident, "exercicio-espolio");
            killed++;
            foreach (var id in sink.Left.Keys) if (!distance.ContainsKey(id)) distance[id] = ticks;
        }

        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>();
        var groundIds = new HashSet<long>(world.Invention?.Leavings?.Select(l => l.Id) ?? []);
        foreach (var (label, lo, hi) in new[] { ("ate6h", 0L, 6L), ("6a12h", 7L, 12L), ("12a24h", 13L, 24L), ("mais24h", 25L, long.MaxValue) })
        {
            int n = 0, fetched = 0, passing = 0, found = 0, ground = 0;
            long hours = 0;
            foreach (var (id, left) in sink.Left)
            {
                long d = distance.GetValueOrDefault(id);
                if (d < lo || d > hi) continue;
                n++;
                if (groundIds.Contains(id)) ground++;
                if (!sink.Collected.TryGetValue(id, out var c)) continue;
                hours += c.Tick - left.Tick;
                if (c.How == "achado") found++; else if (c.Tick == left.Tick) passing++; else fetched++;
            }
            lines.Add(string.Create(inv, $"espolio faixa={label} n={n} recolhido_casa_depois={fetched} recolhido_na_hora={passing} achado={found} no_chao={ground} horas_ate_recolher_media={(fetched + passing + found > 0 ? hours / (double)(fetched + passing + found) : 0):F1}"));
        }
        var t = world.InventionTally;
        lines.Add(string.Create(inv, $"espolio total mortes_provocadas={killed} deixados={t.LeavingsLeft} idas={t.LeavingsFetches} recolhidos={t.LeavingsCollected} achados={t.LeavingsFound} furtados={t.LeavingsStolen} furtos_espolio={sink.GroundThefts} unid={t.LeftUnits} unid_recolhidas={t.CollectedUnits} unid_furtadas={t.StolenUnits} unid_estragadas={t.SpoiledUnits} agua_perdida={t.GroundWaterLost}"));
        var hows = sink.Collected.Values.GroupBy(c => c.How).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}:{g.Count()}");
        lines.Add("espolio modos " + string.Join(" ", hows));
        return lines;
    }
}
