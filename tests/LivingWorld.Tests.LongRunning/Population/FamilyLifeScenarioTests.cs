using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Population.Family;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>AD-136 no mundo rodando (cenário padrão, poucas seeds × 60 anos, um teste só): uniões acabam em
/// vida, irmãos chegam a ser criados em casas diferentes, ninguém se casa dentro do tabu de parentesco e a vila segue viva.</summary>
[Trait("Category", "Century")] // 20 seeds × 100 anos: fora do gate de lote, roda no --full diário
public class FamilyLifeScenarioTests
{
    internal const int Seeds = 4, Years = 60;

    /// <summary>Por seed: o que o teste soma. A vila padrão das seeds 1..4 por 60 anos é a corrida compartilhada do século
    /// (<see cref="DefaultVillageCentury"/>, ADR-0044), que liga este sink e amostra o tally todo mês nesses 60 anos.</summary>
    public sealed record Observed(int SeparationsOrAbandonments, int SiblingPairsApart, int TabooMarriages, int Extinct, int Remarriages);

    internal static Observed Collect(WorldState world, FamilyLifeTally tally, Sink sink) => new(
        (int)(tally.EndedByMode.GetValueOrDefault("separacao") + tally.EndedByMode.GetValueOrDefault("abandono")
            + tally.EndedByMode.GetValueOrDefault("anulacao")),
        tally.SiblingPairsApart, sink.Taboo, world.AliveNpcIndex.Alive.Count == 0 ? 1 : 0, (int)tally.Remarriages);

    internal sealed class Sink(WorldState world, FamilyLifeTally tally) : IWorldEventSink
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
        var perSeed = DefaultVillageCentury.Runs.Take(Seeds).Select(r => r.FamilyLife!).ToArray();
        return new Observed(perSeed.Sum(o => o.SeparationsOrAbandonments), perSeed.Sum(o => o.SiblingPairsApart),
            perSeed.Sum(o => o.TabooMarriages), perSeed.Sum(o => o.Extinct), perSeed.Sum(o => o.Remarriages));
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
