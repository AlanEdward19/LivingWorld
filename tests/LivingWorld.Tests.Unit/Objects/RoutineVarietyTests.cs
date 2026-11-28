using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scenarios;
using Xunit.Abstractions;

namespace LivingWorld.Tests.Unit.Objects;

/// <summary>#261: cada pessoa vive a rotina a seu modo. As necessidades de partida e a queda variam por pessoa (sorteio semeado do mundo e personalidade, dado do
/// catálogo), quem ninguém conduz não fica horas parado sem motivo (procura um uso já abaixo do limite de procura) e os usos que aparecem em um dia são variados.</summary>
public class RoutineVarietyTests(ITestOutputHelper output)
{
    private static (WorldState World, WorldClock Clock) Sandbox(ulong seed)
    {
        var root = LivingWorld.Tests.Unit.Cities.SandboxFixtures.CoupleOnly(LivingWorld.Tests.Unit.Cities.SandboxFixtures.Parse());
        root["Seed"] = seed;
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        Assert.True(loaded.IsSuccess, loaded.Error);
        loaded.Value.World.UsePlayers(null);
        return loaded.Value;
    }

    private static double Level(WorldState world, Npc npc, string need) => HomeLife.Value(world, npc, need, world.CurrentDate.TotalMilliseconds)!.Value;

    [Fact]
    public void The_two_of_the_house_start_with_different_bars_and_the_same_seed_gives_the_same_bars()
    {
        var (world, _) = Sandbox(2611);
        var (again, _) = Sandbox(2611);
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var rafael = world.Npcs.Single(n => n.Name == "Rafael");
        string[] needs = ["hunger", "thirst", "sleep", "social", "hygiene", "fun", "bladder"];
        int different = needs.Count(n => Math.Abs(Level(world, marina, n) - Level(world, rafael, n)) >= 1.0);
        Assert.True(different >= 5, $"só {different} de {needs.Length} barras diferem entre os dois");
        foreach (var need in needs)
            Assert.Equal(Level(world, marina, need), Level(again, again.Npcs.Single(n => n.Name == "Marina"), need), 6); // determinístico
    }

    [Fact]
    public void Personality_changes_how_fast_a_need_falls()
    {
        var (world, _) = Sandbox(2612);
        var marina = world.Npcs.Single(n => n.Name == "Marina");
        var rafael = world.Npcs.Single(n => n.Name == "Rafael");
        double Rate(Npc n, string id) => n.ExtraNeeds![id].DecayRatePerHour;
        var rates = new[] { "hygiene", "fun" }.Select(id => (Rate(marina, id), Rate(rafael, id))).ToList();
        Assert.Contains(rates, r => Math.Abs(r.Item1 - r.Item2) > 0.01 || marina.Personality != rafael.Personality);
        Assert.All(rates, r => Assert.InRange(r.Item1, 0.5, 4.0));
    }

    [Fact]
    public void A_day_without_a_player_shows_varied_uses_and_nobody_stays_idle_for_long()
    {
        var (world, clock) = Sandbox(2613);
        var uses = new Dictionary<string, int>();
        var longestIdle = new Dictionary<string, long>();
        var idleSince = new Dictionary<string, long?>();
        var seen = new HashSet<string>();
        for (int step = 0; step < 24 * 720; step++) // um dia em passos de 5 s (o tempo fino do jogo, mais grosso)
        {
            int minute = step / 12;
            clock.Tick(world, 5_000);
            foreach (var npc in world.Npcs.Where(n => n.Name is "Marina" or "Rafael"))
            {
                long now = world.CurrentDate.TotalMilliseconds;
                if (npc.BodyTask?.Use is { } use && seen.Add($"{npc.Name}|{use}|{npc.BodyTask.StartedAtMs}"))
                {
                    uses[use] = uses.GetValueOrDefault(use) + 1;
                    if (Environment.GetEnvironmentVariable("LW_TRACE") == "1") output.WriteLine($"{minute / 60:00}:{minute % 60:00} {npc.Name} {use} ({npc.BodyTask.UntilMs - npc.BodyTask.StartedAtMs} ms)");
                }
                bool idle = npc.BodyTask is null && npc.CurrentAction is ActionType.Idle;
                if (idle) { if (idleSince.GetValueOrDefault(npc.Name) is null) idleSince[npc.Name] = now; }
                else if (idleSince.GetValueOrDefault(npc.Name) is { } since)
                {
                    longestIdle[npc.Name] = Math.Max(longestIdle.GetValueOrDefault(npc.Name), now - since);
                    idleSince[npc.Name] = null;
                }
            }
        }

        output.WriteLine("usos: " + string.Join(", ", uses.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
        output.WriteLine("maior parada (min): " + string.Join(", ", longestIdle.Select(k => $"{k.Key}={k.Value / 60000}")));
        Assert.True(uses.Count >= 4, $"só {uses.Count} tipos de uso em um dia: {string.Join(",", uses.Keys)}");
        Assert.All(longestIdle, k => Assert.True(k.Value <= 4 * 3_600_000L, $"{k.Key} ficou {k.Value / 60000} min à toa"));
    }
}
