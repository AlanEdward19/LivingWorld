using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;
using static LivingWorld.Tests.Unit.Players.PlayerWalkTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#248: o corpo anda a cada tick pelo rumo mantido, sem esperar comando; para quando o cliente some; não anda sozinho no salto
/// de tempo; o motor devolve o número do último comando; e a máscara que o cliente consulta concorda com o passo do motor.</summary>
public class PlayerContinuousWalkTests
{
    private const int Fine = 50;

    private static (Arm Arm, Npc Npc, PlayerService Service, CellCoord Start, double Mps) Ready(ulong seed, int cells = 12)
    {
        var (arm, npc, service) = Body(seed);
        var w = arm.World;
        var start = OpenRun(w, npc, cells);
        Place(w, npc, start);
        arm.Clock.Tick(w, Fine); // o tempo é fino daqui em diante (como no jogo)
        double mps = PlayerWalk.SpeedMps(w, npc, start, new CellCoord(start.X + 1, start.Y));
        return (arm, npc, service, start, mps);
    }

    [Fact]
    public void The_body_keeps_walking_every_tick_after_a_single_command_and_the_pose_carries_its_number()
    {
        var (arm, npc, service, _, mps) = Ready(1);
        var w = arm.World;
        Assert.True(service.Walk(w, "alan", 1000, 0, false, 7).IsSuccess);
        long x0 = npc.Fine!.X;
        Wait(arm, 400); // 8 ticks, nenhum comando novo
        long expected = (long)Math.Round(mps / w.Players.CellM * 0.4 * FinePose.PerCell);
        Assert.InRange(npc.Fine!.X - x0, expected - 8, expected + 8);
        Assert.Equal(7, npc.Fine.Seq);
        Assert.Equal(w.CurrentDate.TotalMilliseconds, npc.Fine.AtMs); // a pose é a do último tick, não a do último comando
    }

    [Fact]
    public void Without_news_from_the_client_the_body_stops_after_the_idle_limit()
    {
        var (arm, npc, service, _, mps) = Ready(2);
        var w = arm.World;
        Assert.True(service.Walk(w, "alan", 1000, 0, false, 1).IsSuccess);
        long x0 = npc.Fine!.X;
        Wait(arm, 3000);
        Assert.Equal(0, npc.Fine!.HeadingX);
        long limit = (long)Math.Round(mps / w.Players.CellM * (w.Players.WalkIdleStop + 2 * Fine) / 1000.0 * FinePose.PerCell);
        Assert.InRange(npc.Fine.X - x0, 1, limit);
        long still = npc.Fine.X;
        Wait(arm, 500);
        Assert.Equal(still, npc.Fine.X); // parado de vez
    }

    [Fact]
    public void A_command_that_keeps_coming_keeps_the_body_walking_past_the_idle_limit()
    {
        var (arm, npc, service, _, mps) = Ready(3);
        var w = arm.World;
        long x0 = npc.CurrentLocation.X * FinePose.PerCell + 500;
        for (int i = 0; i < 30; i++) // 3 s com o rumo renovado a cada 100 ms
        {
            Assert.True(service.Walk(w, "alan", 1000, 0, false, i + 1).IsSuccess);
            Wait(arm, 100);
        }
        long expected = (long)Math.Round(mps / w.Players.CellM * 3.0 * FinePose.PerCell);
        Assert.InRange(npc.Fine!.X - x0, expected * 9 / 10, expected * 12 / 10); // o chão muda de célula em célula: a velocidade também
        Assert.Equal(30, npc.Fine.Seq);
    }

    [Fact]
    public void A_time_jump_does_not_walk_the_body_by_itself()
    {
        var (arm, npc, service, _, _) = Ready(4);
        var w = arm.World;
        Assert.True(service.Walk(w, "alan", 1000, 0, false, 1).IsSuccess);
        long x0 = npc.Fine!.X;
        for (int i = 0; i < 5; i++) arm.Clock.Tick(w); // ticks de uma hora (salto de tempo, sono acelerado)
        Assert.Equal(x0, npc.Fine!.X);
        Assert.Equal(0, npc.Fine.HeadingX);
    }

    [Fact]
    public void Stopping_with_a_command_stops_the_pump()
    {
        var (arm, npc, service, _, _) = Ready(5);
        var w = arm.World;
        Assert.True(service.Walk(w, "alan", 1000, 0, false, 1).IsSuccess);
        Wait(arm, 200);
        Assert.True(service.Walk(w, "alan", 0, 0, false, 2).IsSuccess);
        long x = npc.Fine!.X;
        Wait(arm, 300);
        Assert.Equal(x, npc.Fine!.X);
        Assert.Equal(2, npc.Fine.Seq);
    }

    [Fact]
    public void The_continuous_walk_is_deterministic_for_the_same_seed()
    {
        long Run(ulong seed)
        {
            var (arm, npc, service, _, _) = Ready(seed);
            for (int i = 0; i < 40; i++)
            {
                if (i % 2 == 0) Assert.True(service.Walk(arm.World, "alan", i < 20 ? 1000 : 700, i < 20 ? 0 : 700, false, i + 1).IsSuccess);
                Wait(arm, 70);
            }
            return npc.Fine!.X * 1_000_003 + npc.Fine.Y;
        }
        Assert.Equal(Run(6), Run(6));
    }

    [Fact]
    public void The_step_mask_agrees_with_the_engine_step_around_a_door_and_a_corner()
    {
        var (arm, npc, _) = Body(1);
        var w = arm.World;
        var house = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value)
            .Select(b => PlayerGround.DoorOf(b)!.Value).First(d => w.Map.TryGetCell(d.Doorstep, out _));
        npc.ExitBuilding();
        Place(w, npc, house.Doorstep);
        const int radius = 5;
        var at = house.Doorstep;
        var mask = PlayerGround.Mask(w, 0, null, at, radius);
        int side = 2 * radius + 1, checkedSteps = 0, blocked = 0;
        for (int j = 0; j < side; j++)
            for (int i = 0; i < side; i++)
            {
                var from = new CellCoord(at.X - radius + i, at.Y - radius + j);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var to = new CellCoord(from.X + dx, from.Y + dy);
                        var step = PlayerGround.Step(w, npc, from, to);
                        if (!step.IsSuccess && step.Error!.Contains("está ali")) continue; // gente é regra à parte (crowdLimit)
                        bool bit = (mask[j * side + i] & (1 << ((dy + 1) * 3 + (dx + 1)))) != 0;
                        Assert.Equal(step.IsSuccess, bit);
                        checkedSteps++;
                        if (!bit) blocked++;
                    }
            }
        Assert.True(checkedSteps > 500 && blocked > 0, $"a janela precisa ter parede e chão ({checkedSteps}, {blocked})");
    }
}
