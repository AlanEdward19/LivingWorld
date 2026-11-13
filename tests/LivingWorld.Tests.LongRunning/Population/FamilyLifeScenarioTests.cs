using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>AD-136 no mundo rodando (cenário padrão, poucas seeds × 60 anos, um teste só): uniões acabam em
/// vida, irmãos chegam a ser criados em casas diferentes, ninguém se casa dentro do tabu de parentesco e a vila segue viva.</summary>
[Trait("Category", "Century")] // 20 seeds × 100 anos: fora do gate de lote, roda no --full diário
public class FamilyLifeScenarioTests
{
    private const int Seeds = 4, Years = 60;

    private sealed record Observed(int SeparationsOrAbandonments, int SiblingPairsApart, int TabooMarriages, int Extinct, int Remarriages);

    private sealed class Sink(WorldState world, FamilyLifeTally tally) : IWorldEventSink
    {
        public int Taboo;

        public void Record(WorldEvent evt)
        {
            tally.Record(evt);
            if (evt.Kind != WorldEventKind.Marriage) return;
            var p = evt.Payload.Split('|');
            var a = world.FindNpc(new NpcId(long.Parse(p[0])));
            var b = world.FindNpc(new NpcId(long.Parse(p[1])));
            if (a is not null && b is not null && Kinship.IsTaboo(world, a, b)) Taboo++;
        }
    }

    private static readonly Lazy<Observed> Run = new(() =>
    {
        int ended = 0, apart = 0, taboo = 0, extinct = 0, remarriages = 0;
        for (ulong seed = 1; seed <= Seeds; seed++)
        {
            var (world, baseClock) = ScenarioRunner.Create(seed);
            var tally = new FamilyLifeTally(world);
            var sink = new Sink(world, tally);
            var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, sink);
            for (int m = 0; m < Years * world.Calendar.MonthsPerYear; m++)
            {
                clock.Run(world, world.Calendar.HoursPerMonth);
                tally.SampleMonth();
            }
            ended += (int)(tally.EndedByMode.GetValueOrDefault("separacao") + tally.EndedByMode.GetValueOrDefault("abandono")
                + tally.EndedByMode.GetValueOrDefault("anulacao"));
            apart += tally.SiblingPairsApart;
            taboo += sink.Taboo;
            remarriages += (int)tally.Remarriages;
            if (world.AliveNpcIndex.Alive.Count == 0) extinct++;
        }
        return new Observed(ended, apart, taboo, extinct, remarriages);
    });

    [Fact]
    [Trait("Category", "Scenario")]
    public void Unions_end_in_life_siblings_grow_up_apart_no_one_marries_within_the_taboo_and_no_village_dies()
    {
        var o = Run.Value;
        Assert.Equal((true, true, true, 0, 0),
            (o.SeparationsOrAbandonments > 0, o.SiblingPairsApart > 0, o.Remarriages > 0, o.TabooMarriages, o.Extinct));
    }
}
