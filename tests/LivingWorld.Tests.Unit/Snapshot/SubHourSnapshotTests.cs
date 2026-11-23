using System.Text.Json.Nodes;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Snapshot;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Snapshot;

/// <summary>ADR-0050 (AD-197b): um mundo parado dentro da hora (Δ menor) salva e reabre sem perder os segundos, pelo JSON e pelo
/// binário posicional; um mundo na hora cheia salva com a forma de antes (só horas), que é a de um save antigo.</summary>
public class SubHourSnapshotTests
{
    [Fact]
    public void A_world_saved_inside_the_hour_reopens_and_keeps_running_like_the_one_that_never_stopped()
    {
        var live = Village(81, initialPopulation: 30);
        live.Clock.Run(live.World, 25, 60);
        long now = live.World.CurrentDate.TotalSeconds;
        new LivingWorld.Simulation.Scheduling.TickContext(live.World, live.World.Rng, live.World.Scheduler)
            .ScheduleEventAtSecond(now + 600, "ninguem", "x"); // evento e despertar dentro da hora também entram no save
        live.World.ReplaceNpcWakeAtSecond(live.World.Npcs.First(n => n.IsAlive).Id.Value, now + 900);

        var fromJson = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(live.World));
        var fromBinary = PositionalSnapshot.DecodeFull(PositionalSnapshot.EncodeFull(live.World, out _));
        foreach (var world in new[] { live.World, fromJson, fromBinary })
            new WorldClock(ScenarioRunner.DefaultSystems()).Run(world, 120, 60);

        Assert.Equal(
            (WorldSnapshot.CanonicalHash(live.World), WorldSnapshot.CanonicalHash(live.World)),
            (WorldSnapshot.CanonicalHash(fromJson), WorldSnapshot.CanonicalHash(fromBinary)));
    }

    [Fact]
    public void A_world_on_the_full_hour_saves_only_hours_like_an_old_save()
    {
        var live = Village(82, initialPopulation: 10);

        var date = JsonNode.Parse(WorldSnapshot.Serialize(live.World))!["CurrentDate"]!.AsObject();

        Assert.Equal((false, live.World.CurrentDate.TotalHours), (date.ContainsKey("SecondOfHour"), date["TotalHours"]!.GetValue<long>()));
    }
}
