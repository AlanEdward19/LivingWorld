using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#241: cortar lenha leva uma jornada; com o mundo só dele, o jogador não espera horas reais: o mundo acelera (o salto do sono,
/// a mesma sequência de ticks mais rápida) até a ação acabar, volta ao tempo real ao terminar, interromper ou se algo o chamar.
/// Com mais de um jogador não há salto.</summary>
public class PlayerTaskSkipTests
{
    private sealed record Host(Arm Arm, SimulationHost Simulation, PlayerService Players, Npc Npc);

    private static Host Cutting(ulong seed, bool secondPlayer = false)
    {
        var arm = Village(seed, initialPopulation: 40);
        var w = arm.World;
        var npc = Pick(w);
        var host = new SimulationHost(new WorldHost(w, arm.Clock));
        var players = new PlayerService(arm.Sink);
        Assert.True(players.Incarnate(w, npc.Id, "alan").IsSuccess);
        if (secondPlayer)
        {
            var other = w.Npcs.First(n => n.IsAlive && n.Id != npc.Id && n.Employer is not null);
            Assert.True(players.Incarnate(w, other.Id, "bia").IsSuccess);
        }
        var tree = w.Map.Cells.Select(c => c.Coord).Where(c => PlayerNature.TreeAt(w, c) is not null).OrderBy(c => c.Y).ThenBy(c => c.X).First();
        Place(w, npc, tree);
        PlayerNature.Take(w, tree, w.CurrentDate.TotalMilliseconds, PlayerNature.Capacity(w, tree) - 2); // sobram duas unidades: 16 h de corte
        var offer = Assert.Single(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
        Assert.True(players.Interact(w, "alan", "nature", offer.Id, "gather").IsSuccess);
        return new Host(arm, host, players, npc);
    }

    [Fact]
    public void Alone_the_world_accelerates_until_the_action_is_done_then_returns_to_real_time()
    {
        var h = Cutting(3);
        var w = h.Arm.World;
        var busy = PlayerTasks.Of(w, h.Npc)!;
        Assert.NotNull(h.Players.Skip);
        Assert.True(h.Players.Skip!.Task);
        int cycles = 0;
        var modes = new HashSet<RhythmMode>();
        while (h.Players.Skip is not null && cycles++ < 200)
        {
            h.Simulation.Cycle(h.Players);
            modes.Add(h.Simulation.LastPlan.Mode);
        }
        Assert.Contains(RhythmMode.Accelerated, modes);
        Assert.InRange(cycles, 2, 24); // ~uma jornada em poucos ticks de uma hora, não em dezenas de milhares de ticks de 50 ms
        Assert.Equal("feito", h.Players.LastWakeReason);
        Assert.True(w.CurrentDate.TotalMilliseconds >= busy.UntilMs);
        Assert.Null(PlayerTasks.Of(w, h.Npc));
        h.Simulation.Cycle(h.Players);
        Assert.Equal(RhythmMode.RealTime, h.Simulation.LastPlan.Mode);
    }

    /// <summary>#243: com o mundo acelerado (ticks de uma hora) cada unidade entra nas mãos no tick em que o instante dela passa: nem antes
    /// (sem entrega adiantada) nem depois.</summary>
    [Fact]
    public void During_the_skip_each_unit_enters_the_hands_at_the_tick_that_passes_its_time()
    {
        var h = Cutting(3);
        var w = h.Arm.World;
        var task = h.Npc.BodyTask!;
        Assert.Equal(2, task.Units);
        Assert.False(h.Npc.IsCarrying);
        var seen = new List<long>();
        for (int cycles = 0; h.Players.Skip is not null && cycles < 200; cycles++)
        {
            h.Simulation.Cycle(h.Players);
            long expected = Math.Min(task.Units, (w.CurrentDate.TotalMilliseconds - task.StartedAtMs) / task.PerUnitMs);
            Assert.Equal(expected, h.Npc.CarriedQuantity);
            seen.Add(h.Npc.CarriedQuantity);
        }
        Assert.Equal(2, h.Npc.CarriedQuantity);
        Assert.Contains(0L, seen.Take(seen.Count - 1)); // houve ciclos com nada, depois com uma, depois com as duas
        Assert.Contains(1L, seen);
    }

    [Fact]
    public void With_two_players_there_is_no_skip_and_the_action_runs_in_normal_time()
    {
        var h = Cutting(3, secondPlayer: true);
        for (int i = 0; i < 20; i++) h.Simulation.Cycle(h.Players);
        Assert.Null(h.Players.Skip);
        Assert.Equal(RhythmMode.RealTime, h.Simulation.LastPlan.Mode);
        Assert.NotNull(PlayerTasks.Of(h.Arm.World, h.Npc)); // segue ocupado: nada de pular
    }

    [Theory]
    [InlineData("fome")]
    [InlineData("sede")]
    [InlineData("chamado")]
    public void An_event_in_the_engine_drops_back_to_real_time_and_says_why(string reason)
    {
        var h = Cutting(3);
        var w = h.Arm.World;
        h.Simulation.Cycle(h.Players);
        Assert.Equal(RhythmMode.Accelerated, h.Simulation.LastPlan.Mode);
        long hour = w.CurrentDate.TotalHours;
        switch (reason)
        {
            case "fome": h.Npc.SetHunger(0, hour); break;
            case "sede": h.Npc.SetThirst(0, hour); break;
            default:
                var friend = w.FindHousehold(h.Npc.Household!.Value)!.Members.Select(w.FindNpc).First(n => n is { IsAlive: true } && n.Id != h.Npc.Id)!;
                friend.MoveTo(h.Npc.CurrentLocation, hour);
                friend.SetCurrentAction(ActionType.Socialize, hour);
                break;
        }
        h.Simulation.Cycle(h.Players);
        h.Simulation.Cycle(h.Players);
        Assert.Null(h.Players.Skip);
        Assert.Equal(reason, h.Players.LastWakeReason);
        Assert.Equal(RhythmMode.RealTime, h.Simulation.LastPlan.Mode);
        Assert.NotNull(PlayerTasks.Of(w, h.Npc)); // a ação continua, só que no tempo normal
    }

    [Fact]
    public void Interrupting_the_action_releases_the_skip()
    {
        var h = Cutting(3);
        h.Simulation.Cycle(h.Players);
        Assert.NotNull(h.Players.Skip);
        h.Players.Walk(h.Arm.World, "alan", 0, 0); // o cliente sincroniza a pose por aqui (recusado ocupado): não solta o salto
        Assert.NotNull(h.Players.Skip);
        Assert.True(h.Players.Interrupt(h.Arm.World, "alan").IsSuccess);
        Assert.Null(h.Players.Skip);
        h.Simulation.Cycle(h.Players);
        Assert.Equal(RhythmMode.RealTime, h.Simulation.LastPlan.Mode);
    }

    [Fact]
    public void The_skip_is_the_same_tick_sequence_only_faster_so_the_world_matches_plain_hour_ticks()
    {
        var skipped = Cutting(5);
        int ticks = 0;
        while (skipped.Players.Skip is not null && ticks < 200)
        {
            long before = skipped.Arm.World.CurrentDate.TotalMilliseconds;
            skipped.Simulation.Cycle(skipped.Players);
            if (skipped.Arm.World.CurrentDate.TotalMilliseconds - before == WorldMillis.HourTick) ticks++; // os ticks do salto, de uma hora
        }
        var plain = Cutting(5);
        Assert.True(plain.Players.SetTickDelta(plain.Arm.World, "host", WorldMillis.HourTick).IsSuccess);
        for (int i = 0; i < ticks; i++) plain.Arm.Clock.Tick(plain.Arm.World, WorldMillis.HourTick);
        int fine = plain.Arm.World.Players.Time.FineDeltaMs(); // o ciclo que acaba o salto já anda no tempo real
        plain.Players.EndSkip(plain.Arm.World, "feito");
        Assert.True(plain.Players.SetTickDelta(plain.Arm.World, "host", fine).IsSuccess);
        plain.Arm.Clock.Tick(plain.Arm.World, fine);
        // o salto tem de ter sido tick a tick: o mesmo número de ticks de uma hora dá o mesmo mundo (nada de estado pulado)
        Assert.Equal(WorldSnapshot.CanonicalHash(plain.Arm.World), WorldSnapshot.CanonicalHash(skipped.Arm.World));
        Assert.True(ticks >= 2);
    }
}
