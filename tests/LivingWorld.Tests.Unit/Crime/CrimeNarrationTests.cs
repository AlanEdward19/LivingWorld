using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using static LivingWorld.Tests.Unit.Crime.CrimeTestWorld;

namespace LivingWorld.Tests.Unit.Crime;

/// <summary>AD-150: o cliente mostra crime e justiça como acontecem (pedido do dono, "totalmente explícito"): nomes de autor e
/// vítima, motivo, bem, desfecho e punição, e a verdade do motor (o furto que ninguém viu, o condenado inocente).</summary>
public class CrimeNarrationTests
{
    private static Npc Named(WorldState world, string name)
    {
        var npc = new Npc(world.NextNpcIdAndAdvance(), name, Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1),
            Here, null, null, household: null, health: 100, personality: Average, profession: new ProfessionType(1), currentLocation: Here);
        world.AddNpc(npc);
        return npc;
    }

    [Fact]
    public void Theft_assault_and_verdict_are_told_with_names_motive_good_outcome_and_punishment()
    {
        var world = New();
        var ana = Named(world, "Ana");
        var rui = Named(world, "Rui");
        string Tell(WorldEventKind kind, string payload) => CrimeEventNarrator.Describe(world, new WorldEvent(0, kind, payload, 1, null, "crime"))!;

        Assert.Equal("Ana furtou 4 de comida da casa de Rui (por fome, valor 8); ninguém viu",
            Tell(WorldEventKind.TheftCommitted, $"{ana.Id.Value}|{rui.Id.Value}|casa|fome|comida|4|8|levou|0|-1"));
        Assert.Equal("Rui agrediu Ana (por vingança): morreu; 2 viram",
            Tell(WorldEventKind.Assault, $"{rui.Id.Value}|{ana.Id.Value}|vinganca|30|morto|2|5"));
        Assert.Equal("Ana foi condenado por furto: açoite no pelourinho (não tinha como pagar) — inocente de verdade",
            Tell(WorldEventKind.Verdict, $"{ana.Id.Value}|{rui.Id.Value}|3|furto|culpado|castigo|0|0"));
        Assert.Null(CrimeEventNarrator.Describe(world, new WorldEvent(0, WorldEventKind.Birth, "1", 1, null, "x")));
        Assert.All(new[] { WorldEventKind.TheftCommitted, WorldEventKind.Assault, WorldEventKind.Homicide, WorldEventKind.Verdict,
            WorldEventKind.CrimeReported, WorldEventKind.Executed, WorldEventKind.Banished, WorldEventKind.InheritanceDisputed },
            k => Assert.Contains(k, LivingEventPresentationCatalog.MappedKinds));
    }
}
