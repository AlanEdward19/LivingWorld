using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;
using static LivingWorld.Tests.Unit.Players.PlayerNatureTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#243: o padrão único de extração. A madeira sai unidade a unidade no ritmo da tarefa (cada unidade nas mãos no instante em que
/// sai), parar deixa o que já saiu, a árvore é estado do mundo e finito (esgota e vira toco, volta a crescer), só as tocadas ficam no
/// livro (e no hash/snapshot) e o toco não oferece nada.</summary>
public class TreeYieldTests
{
    private static BodyTask Start(Arm arm, PlayerService service, out InteractionOffer offer)
    {
        offer = Assert.Single(PlayerService.Reach(arm.World, "alan")!, o => o.Kind == "nature");
        Assert.True(service.Interact(arm.World, "alan", "nature", offer.Id, "gather").IsSuccess);
        return arm.World.Npcs.First(n => n.ControllerId == "alan").BodyTask!;
    }

    [Fact]
    public void Units_reach_the_hands_one_at_a_time_at_the_pace_of_the_task_and_come_out_of_the_tree()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        long full = PlayerNature.Capacity(w, at);
        PlayerNature.Take(w, at, w.CurrentDate.TotalMilliseconds, full - 3); // sobram três: o corpo não passa dias sem beber
        var task = Start(arm, service, out _);
        Assert.True(task.PerUnitMs > 0 && task.Units == 3);
        long lastCarried = 0;
        while (PlayerTasks.Of(w, npc) is not null)
        {
            arm.Clock.Tick(w, 600_000);
            long expected = Math.Min(task.Units, (w.CurrentDate.TotalMilliseconds - task.StartedAtMs) / task.PerUnitMs);
            Assert.Equal(expected, npc.CarriedQuantity); // a unidade k está nas mãos em início + k × ritmo, nem antes nem depois
            Assert.Equal(3 - expected, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds));
            Assert.InRange(npc.CarriedQuantity - lastCarried, 0, 1);
            lastCarried = npc.CarriedQuantity;
        }
        Assert.Equal(task.Units, npc.CarriedQuantity);
        Assert.Null(npc.BodyTask); // a última unidade encerra a tarefa
        Assert.Equal(0, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds)); // as três saíram: toco
        var touched = Assert.Single(PlayerNature.Touched(w));
        Assert.Equal((at, 0L, full), touched);
    }

    [Fact]
    public void Stopping_after_one_unit_leaves_that_unit_and_a_cut_tree()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        long cap = PlayerNature.Capacity(w, at);
        Start(arm, service, out _);
        while (npc.CarriedQuantity < 1) arm.Clock.Tick(w, 600_000);
        Assert.True(service.Interrupt(w, "alan").IsSuccess);
        Assert.Equal(1, npc.CarriedQuantity);
        Assert.Equal(cap - 1, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds));
        Assert.Equal("cut", PlayerNature.StageOf(cap - 1, cap));
        arm.Clock.Run(w, 12); // o evento agendado das próximas unidades chega e não faz nada: a ação foi interrompida
        Assert.Equal(1, npc.CarriedQuantity);
        // com 1 na mão e espaço, a mesma árvore oferece de novo (a sessão cabe no que resta das mãos)
        Assert.Contains(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature");
    }

    [Fact]
    public void The_tree_runs_out_becomes_a_stump_and_the_stump_offers_nothing()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        long cap = PlayerNature.Capacity(w, at);
        PlayerNature.Take(w, at, w.CurrentDate.TotalMilliseconds, cap - 2); // sobram duas unidades
        var task = Start(arm, service, out var offer);
        Assert.Equal(2, task.Units); // a sessão nunca passa da madeira que resta
        while (PlayerTasks.Of(w, npc) is not null) arm.Clock.Tick(w, 600_000);
        Assert.Equal(2, npc.CarriedQuantity);
        Assert.Equal(0, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds));
        var stump = Assert.Single(PlayerNature.Touched(w));
        Assert.Equal("stump", PlayerNature.StageOf(stump.Left, stump.Capacity));
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature" && o.X == at.X && o.Y == at.Y); // toco não oferece
        string hash = WorldSnapshot.CanonicalHash(w);
        Assert.False(service.Interact(w, "alan", "nature", offer.Id, "gather").IsSuccess);
        Assert.Equal(hash, WorldSnapshot.CanonicalHash(w));
    }

    [Fact]
    public void The_tree_grows_back_with_time_and_leaves_the_book_when_whole()
    {
        var (arm, _, _, at) = InTheWoods(3);
        var w = arm.World;
        long cap = PlayerNature.Capacity(w, at), rate = PlayerNature.RegrowMsPerUnit(w), now = w.CurrentDate.TotalMilliseconds;
        Assert.Equal(cap, PlayerNature.Take(w, at, now, cap + 5)); // derrubada: não se tira mais do que tem
        Assert.Equal(0, PlayerNature.Left(w, at, now));
        Assert.Equal(0, PlayerNature.Left(w, at, now + rate - 1));
        Assert.Equal(1, PlayerNature.Left(w, at, now + rate)); // uma unidade a cada 30 dias
        Assert.Equal(cap, PlayerNature.Left(w, at, now + rate * cap * 2)); // nunca passa da árvore inteira
        var other = w.Map.Cells.Select(c => c.Coord).Where(c => c != at && PlayerNature.TreeAt(w, c) is not null).OrderBy(c => c.Y).ThenBy(c => c.X).First();
        PlayerNature.Take(w, other, now + rate * cap * 2, 1); // o próximo corte, em qualquer árvore, limpa as que já voltaram
        Assert.Null(w.Trees!.Find(at.X, at.Y));
    }

    [Fact]
    public void A_stump_regrows_one_unit_per_thirty_days_of_the_running_clock()
    {
        var (arm, _, _, at) = InTheWoods(3);
        var w = arm.World;
        PlayerNature.Take(w, at, w.CurrentDate.TotalMilliseconds, PlayerNature.Capacity(w, at));
        Assert.DoesNotContain(PlayerService.Reach(w, "alan")!, o => o.Kind == "nature" && o.X == at.X && o.Y == at.Y);
        arm.Clock.Run(w, w.Calendar.HoursPerDay * 30 - 1);
        Assert.Equal(0, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds));
        arm.Clock.Run(w, 2);
        Assert.Equal(1, PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds));
    }

    [Fact]
    public void Only_touched_trees_are_in_the_snapshot_and_the_hash_survives_a_round_trip()
    {
        var (arm, npc, service, at) = InTheWoods(3);
        var w = arm.World;
        Assert.Null(w.Trees); // mundo sem corte: nada no livro (hash de antes)
        string untouched = WorldSnapshot.CanonicalHash(w);
        Start(arm, service, out _);
        while (PlayerTasks.Of(w, npc) is not null) arm.Clock.Tick(w, 600_000);
        Assert.NotNull(w.Trees);
        Assert.Single(w.Trees!.Cuts);
        Assert.NotEqual(untouched, WorldSnapshot.CanonicalHash(w));
        var back = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(w));
        Assert.Equal(WorldSnapshot.CanonicalHash(w), WorldSnapshot.CanonicalHash(back));
        Assert.Equal(PlayerNature.Left(w, at, w.CurrentDate.TotalMilliseconds), PlayerNature.Left(back, at, back.CurrentDate.TotalMilliseconds));
    }

    [Fact]
    public void Cutting_in_time_is_the_same_world_with_the_same_seed_and_different_with_another()
    {
        string Run(ulong seed, int stopAfterMinutes)
        {
            var (arm, npc, service, _) = InTheWoods(seed);
            Start(arm, service, out _);
            arm.Clock.Run(arm.World, stopAfterMinutes, 600_000);
            if (PlayerTasks.Of(arm.World, npc) is not null) Assert.True(service.Interrupt(arm.World, "alan").IsSuccess);
            return WorldSnapshot.CanonicalHash(arm.World);
        }
        Assert.Equal(Run(5, 60), Run(5, 60));
        Assert.NotEqual(Run(5, 60), Run(5, 3));
    }
}
