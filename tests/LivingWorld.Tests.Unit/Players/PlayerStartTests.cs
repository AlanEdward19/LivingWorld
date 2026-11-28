using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerControlTests;
using static LivingWorld.Tests.Unit.Players.PlayerWalkTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#238: o ponto de partida do jogador (casa, família, despensa, emprego, mata, o que aperta) e o que atrasa o passo, tudo lido
/// do motor para o cliente montar o guia da primeira posse e o rótulo de chão sem inventar nada.</summary>
public class PlayerStartTests
{
    private static void Satisfy(WorldState w, Npc npc)
    {
        long now = w.CurrentDate.TotalHours;
        npc.SetHunger(100, now);
        npc.SetThirst(100, now);
        npc.SetSleep(100, now);
        npc.SetSocial(100, now);
    }

    [Fact]
    public void The_start_names_the_home_the_housemates_and_the_pantry_from_the_household()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        var home = w.FindHousehold(npc.Household!.Value)!;
        home.Deposit(new ResourceType(w.EconomyRules.FoodResourceId), 4);
        var start = PlayerStartQuery.Of(w, npc);

        Assert.False(start.Homeless);
        Assert.NotNull(start.Home);
        Assert.Equal((home.Location.X, home.Location.Y), (start.Home!.X, start.Home.Y));
        var expected = home.Members.Where(m => m != npc.Id).Select(m => m.Value).Order().ToList();
        Assert.Equal(expected, start.Home.Members.Select(m => m.Id).Order().ToList());
        Assert.Equal(expected.Count == 0, start.Home.Alone);
        Assert.Contains(start.Home.Pantry, r => r.ResourceId == w.EconomyRules.FoodResourceId && r.Quantity >= 4);
        foreach (var member in start.Home.Members.Where(m => new NpcId(m.Id) == npc.Spouse))
            Assert.Equal("spouse", member.Relation);
    }

    [Fact]
    public void What_presses_follows_the_emptiest_need_and_is_none_when_everything_is_met()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        Satisfy(w, npc);
        Assert.NotEqual("hunger", PlayerStartQuery.Of(w, npc).Pressing);
        npc.SetHunger(5, w.CurrentDate.TotalHours);
        Assert.Equal("hunger", PlayerStartQuery.Of(w, npc).Pressing);
        Satisfy(w, npc);
        npc.SetThirst(5, w.CurrentDate.TotalHours);
        Assert.Equal("thirst", PlayerStartQuery.Of(w, npc).Pressing);
    }

    [Fact]
    public void The_job_is_the_employers_place_and_the_start_is_deterministic()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        var a = PlayerStartQuery.Of(w, npc);
        var b = PlayerStartQuery.Of(w, npc);
        Assert.Equal(a.Pressing, b.Pressing);
        Assert.Equal(a.Wild, b.Wild);
        if (npc.Employer is { } e)
            Assert.Equal((w.FindWorkplace(e)!.Location.X, w.FindWorkplace(e)!.Location.Y), (a.Job!.X, a.Job.Y));
        else
            Assert.Null(a.Job);
        if (a.Wild is { } wild)
            Assert.True(PlayerNature.IsWild(w, w.Map.CellAt(new CellCoord(wild.X, wild.Y)),
                new ResourceType(w.StreetRules.HouseMaterialResourceId)));
    }

    [Fact]
    public void A_tired_body_is_slowed_by_sleep_and_the_drag_says_so_with_the_terrain_under_it()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        var start = OpenRun(w, npc, 3);
        Place(w, npc, start);
        var ahead = new CellCoord(start.X + 1, start.Y);
        Satisfy(w, npc);
        var rested = PlayerWalk.DragOf(w, npc, start, ahead);
        Assert.Equal(w.Map.CellAt(start).Terrain.Id, rested.Terrain);
        Assert.NotEqual("tired", rested.Cause);

        npc.SetSleep(0, w.CurrentDate.TotalHours);
        var tired = PlayerWalk.DragOf(w, npc, start, ahead);
        Assert.True(tired.SlowPct >= 5);
        Assert.Equal("tired", tired.Cause);
        Assert.True(tired.SlowPct > rested.SlowPct);
    }

    [Fact]
    public void The_terrain_weight_is_its_ratio_to_the_lightest_terrain_of_the_map()
    {
        var (arm, npc, _) = Body(3);
        var w = arm.World;
        double min = w.Map.Cost.TerrainWeight.Values.Min();
        foreach (var cell in w.Map.Cells.Take(400))
        {
            var next = new CellCoord(cell.Coord.X + 1, cell.Coord.Y);
            if (!w.Map.TryGetCell(next, out _)) continue;
            var drag = PlayerWalk.DragOf(w, npc, cell.Coord, next);
            Assert.Equal((int)Math.Round((w.Map.Cost.WeightOf(cell.Terrain) / min - 1.0) * 100), drag.TerrainPct);
        }
    }
}
