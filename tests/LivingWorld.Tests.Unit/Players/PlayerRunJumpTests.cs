using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Infrastructure.EventLog;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scenarios;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;
using static LivingWorld.Tests.Unit.Players.PlayerWalkTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>AD-197u: a célula comporta gente (<see cref="PlayerRules.CellCrowd"/>), a velocidade do passo não carrega o peso de
/// terreno do mapa, correr e pular são decisões do motor sobre o corpo.</summary>
public class PlayerRunJumpTests
{
    private const int Fine = 50;

    private static List<Npc> Others(WorldState w, Npc self, int count) =>
        [.. w.AliveNpcIndex.Alive.Where(n => n.Id != self.Id && n.City == self.City).OrderBy(n => n.Id.Value).Take(count)];

    [Fact]
    public void A_cell_takes_a_crowd_and_only_bars_the_step_when_it_is_full_with_the_name_of_who_is_there()
    {
        var (arm, npc, _) = Body(1);
        var w = arm.World;
        var start = OpenRun(w, npc, 3);
        Place(w, npc, start);
        var target = new CellCoord(start.X + 1, start.Y);
        var others = Others(w, npc, w.Players.CellCrowd);
        Assert.Equal(3, w.Players.CellCrowd);

        for (int i = 0; i < others.Count - 1; i++)
        {
            Place(w, others[i], target);
            Assert.True(PlayerGround.Step(w, npc, start, target).IsSuccess, $"com {i + 1} na célula ainda cabe mais um");
        }
        Place(w, others[^1], target);
        var full = PlayerGround.Step(w, npc, start, target);
        Assert.False(full.IsSuccess);
        Assert.Contains(others.OrderBy(o => o.Id.Value).First().Name, full.Error!); // a recusa diz quem está ali
        Assert.Equal(others.Count, PlayerGround.CrowdAt(w, npc, target, 0, null, out _));
    }

    [Fact]
    public void A_cell_limit_of_one_keeps_the_old_rule_that_one_person_bars_the_cell()
    {
        var (arm, npc, _) = Body(1, rules: PlayerControlTests.NoPace with { CellCrowdLimit = 1 });
        var w = arm.World;
        var start = OpenRun(w, npc, 3);
        Place(w, npc, start);
        var target = new CellCoord(start.X + 1, start.Y);
        Place(w, Others(w, npc, 1)[0], target);
        Assert.False(PlayerGround.Step(w, npc, start, target).IsSuccess);
    }

    [Fact]
    public void A_rested_healthy_adult_walks_at_human_speed_on_every_terrain_and_tiredness_and_wounds_still_brake()
    {
        var seen = new HashSet<int>();
        var all = new List<double>();
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var (arm, npc, _) = Body(seed);
            var w = arm.World;
            npc.SetSleep(90, w.CurrentDate.TotalHours);
            var homes = w.Households.Select(h => h.Location).ToHashSet();
            foreach (var cell in w.Map.Cells.Select(c => c.Coord).OrderBy(c => c.X).ThenBy(c => c.Y))
            {
                var east = new CellCoord(cell.X + 1, cell.Y);
                if (!w.Map.TryGetCell(east, out var m2) || m2.HasWater || w.Map.CellAt(cell).HasWater || PlayerGround.At(w, cell) is not null
                    || PlayerGround.At(w, east) is not null || homes.Contains(cell) || homes.Contains(east)) continue;
                int terrain = w.Map.CellAt(cell).Terrain.Id;
                if (!seen.Add(terrain * 100 + (int)seed)) continue;
                double mps = PlayerWalk.SpeedMps(w, npc, cell, east);
                Assert.InRange(mps, 0.9, 1.8); // terra, mata ou morro (chuva e rua a tiram um pouco): o corpo descansado anda como gente
                all.Add(mps);
            }
        }
        all.Sort();
        Assert.InRange(all[all.Count / 2], 1.2, 1.5); // a mediana é a do adulto descansado: ~1,3 m/s
        Assert.True(seen.Select(k => k / 100).Distinct().Count() >= 2, "mais de um terreno medido");

        var (arm2, npc2, _) = Body(1);
        var w2 = arm2.World;
        var from = OpenRun(w2, npc2, 2);
        var to = new CellCoord(from.X + 1, from.Y);
        Place(w2, npc2, from);
        npc2.SetSleep(90, w2.CurrentDate.TotalHours);
        double rested = PlayerWalk.SpeedMps(w2, npc2, from, to);
        npc2.SetSleep(10, w2.CurrentDate.TotalHours);
        double tired = PlayerWalk.SpeedMps(w2, npc2, from, to);
        npc2.SetSleep(90, w2.CurrentDate.TotalHours);
        npc2.SetHealth(15);
        double wounded = PlayerWalk.SpeedMps(w2, npc2, from, to);
        Assert.True(tired < rested * 0.85, $"cansado {tired:0.00} < descansado {rested:0.00}");
        Assert.True(wounded < rested * 0.95, $"ferido {wounded:0.00} < descansado {rested:0.00}");
    }

    [Fact]
    public void Running_is_faster_costs_more_sleep_and_the_pose_remembers_it()
    {
        double walkMps = 0, runMps = 0, walkDrop = 0, runDrop = 0;
        foreach (bool run in new[] { false, true })
        {
            var (arm, npc, service) = Body(1);
            var w = arm.World;
            var start = OpenRun(w, npc, 12);
            Place(w, npc, start);
            npc.SetSleep(90, w.CurrentDate.TotalHours);
            double slept = npc.SleepNeed.ValueAtMs(w.CurrentDate.TotalMilliseconds);
            walkMps = run ? walkMps : PlayerWalk.SpeedMps(w, npc, start, new CellCoord(start.X + 1, start.Y));
            double mps = PlayerWalk.SpeedMps(w, npc, start, new CellCoord(start.X + 1, start.Y), run);
            Assert.True(service.Walk(w, "alan", 1000, 0, run).IsSuccess);
            Assert.Equal(run, npc.Fine!.Run);
            long x0 = npc.Fine.X;
            for (int i = 0; i < 6; i++)
            {
                Wait(arm, 150);
                Assert.True(service.Walk(w, "alan", 1000, 0, run).IsSuccess);
            }
            long expected = (long)Math.Round(mps / w.Players.CellM * 0.9 * FinePose.PerCell);
            Assert.InRange(npc.Fine!.X - x0, (long)(expected * 0.94), (long)(expected * 1.06));
            double drop = slept - npc.SleepNeed.ValueAtMs(w.CurrentDate.TotalMilliseconds);
            if (run) { runMps = mps; runDrop = drop; } else walkDrop = drop;
        }
        Assert.InRange(runMps / walkMps, 2.0, 2.3);
        Assert.True(runMps <= 4.5 + 1e-9);
        Assert.True(runDrop > walkDrop * 2, $"correr gasta mais fôlego ({runDrop:0.0000} > {walkDrop:0.0000})");
    }

    [Fact]
    public void A_tired_or_wounded_body_walks_instead_of_running_and_stopping_clears_the_run()
    {
        var (arm, npc, service) = Body(1);
        var w = arm.World;
        Place(w, npc, OpenRun(w, npc, 4));
        npc.SetSleep(30, w.CurrentDate.TotalHours); // abaixo de Tired
        Assert.True(service.Walk(w, "alan", 1000, 0, true).IsSuccess);
        Assert.False(npc.Fine!.Run);
        npc.SetSleep(90, w.CurrentDate.TotalHours);
        npc.SetHealth(w.Players.ExertionMinHealth - 1);
        Assert.True(service.Walk(w, "alan", 1000, 0, true).IsSuccess);
        Assert.False(npc.Fine!.Run);
        npc.SetHealth(100);
        Assert.True(service.Walk(w, "alan", 1000, 0, true).IsSuccess);
        Assert.True(npc.Fine!.Run);
        Assert.True(service.Walk(w, "alan", 0, 0, true).IsSuccess);
        Assert.False(npc.Fine!.Run); // parado não corre
    }

    [Fact]
    public void Jumping_stays_in_the_air_for_a_body_time_spends_sleep_and_refuses_a_second_jump_in_the_air()
    {
        var (arm, npc, service) = Body(2);
        var w = arm.World;
        Place(w, npc, OpenRun(w, npc, 2));
        npc.SetSleep(90, w.CurrentDate.TotalHours);
        var at = npc.CurrentLocation;
        long now = w.CurrentDate.TotalMilliseconds;
        double before = npc.SleepNeed.ValueAtMs(now);

        var jump = service.Jump(w, "alan");
        Assert.True(jump.IsSuccess, jump.Error);
        Assert.InRange(jump.Value, 400, 600);
        Assert.Equal(now + jump.Value, npc.JumpUntilMs);
        Assert.Equal(at, npc.CurrentLocation); // sem efeito na grade
        Assert.True(before - npc.SleepNeed.ValueAtMs(now) >= w.Players.JumpSleepCost - 1e-9);
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Jump);

        int logged = service.Log.Records.Count;
        var again = service.Jump(w, "alan");
        Assert.False(again.IsSuccess);
        Assert.StartsWith(PlayerCommands.Conflict, again.Error);
        Assert.Contains("no ar", again.Error);
        Assert.Equal(logged, service.Log.Records.Count); // a recusa não entra no registro

        Wait(arm, jump.Value + Fine);
        Assert.True(service.Jump(w, "alan").IsSuccess);
    }

    [Fact]
    public void Jumping_is_refused_for_an_exhausted_wounded_tiny_or_very_old_body_and_for_who_controls_nobody()
    {
        var (arm, npc, service) = Body(1);
        var w = arm.World;
        npc.SetSleep(5, w.CurrentDate.TotalHours);
        Assert.Contains("exausto", service.Jump(w, "alan").Error);
        npc.SetSleep(90, w.CurrentDate.TotalHours);
        npc.SetHealth(w.Players.ExertionMinHealth - 1);
        Assert.Contains("ferido", service.Jump(w, "alan").Error);
        npc.SetHealth(100);
        Assert.True(service.Jump(w, "alan").IsSuccess);
        Assert.StartsWith(PlayerCommands.NotFound, service.Jump(w, "ninguem").Error);

        foreach (var (min, max) in new[] { (0, 2), (90, 120) })
        {
            var body = w.AliveNpcIndex.Alive.FirstOrDefault(n => n.ControllerId is null
                && n.AgeYears(w.CurrentDate) >= min && n.AgeYears(w.CurrentDate) <= max);
            if (body is null) continue; // esta semente não tem alguém nessa idade
            Assert.NotNull(PlayerJump.Refusal(w, body));
        }
        var adult = w.AliveNpcIndex.Alive.First(n => n.ControllerId is null && n.AgeYears(w.CurrentDate) is >= 25 and <= 45 && n.Health >= 90);
        adult.SetSleep(90, w.CurrentDate.TotalHours);
        Assert.Null(PlayerJump.Refusal(w, adult));
    }

    [Fact]
    public void Jumping_to_exhaustion_drains_until_the_engine_refuses()
    {
        var (arm, npc, service) = Body(2);
        var w = arm.World;
        Place(w, npc, OpenRun(w, npc, 2));
        npc.SetSleep(17, w.CurrentDate.TotalHours); // perto do piso de JumpMinSleep (15)
        int jumps = 0;
        for (int i = 0; i < 20; i++)
        {
            var r = service.Jump(w, "alan");
            if (!r.IsSuccess) { Assert.Contains("exausto", r.Error); break; }
            jumps++;
            Wait(arm, r.Value + Fine);
        }
        Assert.InRange(jumps, 1, 19);
    }

    [Fact]
    public void A_log_with_running_and_jumping_replays_to_the_same_hash_and_snapshots_keep_the_new_state()
    {
        var arm = Village(3, NoPace);
        var w = arm.World;
        var npc = Pick(w);
        var start = OpenRun(w, npc, 10);
        Place(w, npc, start);
        string snapshot = WorldSnapshot.Serialize(w);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.True(service.SetTickDelta(w, "admin", Fine).IsSuccess);
        for (int i = 0; i < 24; i++)
        {
            Assert.True(service.Walk(w, "alan", 1000, 0, i % 3 != 0).IsSuccess);
            if (i == 5 || i == 20) service.Jump(w, "alan");
            for (int t = 0; t < 3; t++) arm.Clock.Tick(w, Fine);
        }
        Assert.True(service.Walk(w, "alan", 1000, 0, true).IsSuccess);
        long end = w.CurrentDate.TotalMilliseconds;
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Jump);
        Assert.Contains(service.Log.Records, r => r.Kind == PlayerCommandKind.Walk && r.Run == true);
        Assert.True(npc.JumpUntilMs > 0);

        var replayed = PlayerReplay.Run(snapshot, service.Log.Records,
            new WorldClock(ScenarioRunner.DefaultSystems(), sink: new BufferingWorldEventSink()), new BufferingWorldEventSink(), end);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(replayed));
        Assert.Equal(npc.Fine, replayed.FindNpc(npc.Id)!.Fine);
        Assert.Equal(npc.JumpUntilMs, replayed.FindNpc(npc.Id)!.JumpUntilMs);

        var restored = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(w));
        Assert.Equal(npc.JumpUntilMs, restored.FindNpc(npc.Id)!.JumpUntilMs);
        Assert.Equal(npc.Fine, restored.FindNpc(npc.Id)!.Fine);
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(restored));
    }

    [Fact]
    public void A_world_that_never_ran_or_jumped_keeps_the_snapshot_bytes_of_before()
    {
        var arm = Village(3, NoPace);
        string json = WorldSnapshot.Serialize(arm.World);
        Assert.DoesNotContain("JumpUntilMs", json);
        Assert.DoesNotContain("\"Run\"", json);
        Assert.DoesNotContain("CellCrowdLimit", json);
    }
}
