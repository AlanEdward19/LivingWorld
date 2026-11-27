using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#227: o corpo possuído que o motor guardou numa célula de parede (a origem do prédio, "em casa" sem cômodo) sai para o
/// piso com caminho até a porta, e anda.</summary>
public class PlayerSettleTests
{
    private static readonly CellCoord[] Four = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    /// <summary>Um prédio posicionado e concluído e uma célula de parede do térreo dele (a de menor linha e coluna), em coordenadas do mundo.</summary>
    private static (Building House, CellCoord Wall) WallOf(WorldState w)
    {
        var house = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value).First();
        var origin = house.Position!.Value;
        var wall = BuildingFootprintGenerator.Generate(house).Where(c => !InteriorWalkability.IsWalkable(c.Material))
            .OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X).First().Cell;
        return (house, new CellCoord(origin.X + wall.X, origin.Y + wall.Y));
    }

    private static (Arm Arm, Npc Npc, PlayerService Service, Building House) InAWall(ulong seed)
    {
        var arm = PlayerAbsenceTests.Village(seed, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        var service = new PlayerService(arm.Sink);
        var (house, wall) = WallOf(arm.World);
        npc.ExitBuilding();
        npc.MoveTo(wall, arm.World.CurrentDate.TotalHours); // o "em casa" sem Interior: a pose é a célula sólida
        Assert.False(PlayerGround.Walkable(arm.World, PlayerGround.PoseOf(arm.World, npc)));
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan").IsSuccess);
        return (arm, npc, service, house);
    }

    [Fact]
    public void Possessing_a_body_in_a_wall_cell_puts_it_on_walkable_floor_with_a_way_to_the_door()
    {
        var (arm, npc, _, house) = InAWall(1);
        var w = arm.World;
        var pose = PlayerGround.PoseOf(w, npc);
        Assert.True(PlayerGround.Walkable(w, pose));
        var spot = PlayerGround.At(w, pose);
        if (spot is null) return; // planta sem piso: a soleira
        Assert.Equal(house.Id, spot.Value.Building.Id);
        var seen = new HashSet<CellCoord> { pose };
        var queue = new Queue<CellCoord>([pose]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var d in Four)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (PlayerGround.At(w, next) is { } s && s.Building.Id == house.Id && PlayerGround.Walkable(w, next) && seen.Add(next))
                    queue.Enqueue(next);
            }
        }

        Assert.Contains(spot.Value.Door, seen);
    }

    [Fact]
    public void After_settling_the_next_steps_work_and_the_way_out_through_the_door_leaves_the_building()
    {
        var (arm, npc, service, house) = InAWall(1);
        var w = arm.World;
        var (door, doorstep) = PlayerGround.DoorOf(house)!.Value;
        int guard = 0;
        while (PlayerGround.PoseOf(w, npc) != door && PlayerGround.PoseOf(w, npc) != doorstep && guard++ < 200)
        {
            var from = PlayerGround.PoseOf(w, npc);
            int Dist(CellCoord c) => Math.Abs(c.X - door.X) + Math.Abs(c.Y - door.Y);
            var step = Four.Select(d => new CellCoord(from.X + d.X, from.Y + d.Y))
                .Where(c => Dist(c) < Dist(from) && PlayerGround.Step(w, npc, from, c).IsSuccess).First();
            Assert.True(service.Move(w, "alan", step.X - from.X, step.Y - from.Y).IsSuccess);
        }

        if (PlayerGround.PoseOf(w, npc) == door)
            Assert.True(service.Move(w, "alan", doorstep.X - door.X, doorstep.Y - door.Y).IsSuccess);
        Assert.Equal(doorstep, PlayerGround.PoseOf(w, npc));
        Assert.Null(PlayerGround.At(w, doorstep)); // fora: pose sem prédio
    }

    [Fact]
    public void The_settling_is_deterministic_and_leaves_a_body_that_was_not_in_a_wall_where_it_was()
    {
        Assert.Equal(InAWall(1).Npc.CurrentLocation, InAWall(1).Npc.CurrentLocation); // mesma seed, mesmo lugar: sem RNG
        var arm = PlayerAbsenceTests.Village(1, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        npc.ExitBuilding();
        var open = PlayerWalkTests.OpenRun(arm.World, npc, 1);
        npc.MoveTo(open, arm.World.CurrentDate.TotalHours);
        Assert.True(new PlayerService(arm.Sink).Incarnate(arm.World, npc.Id, "alan").IsSuccess);
        Assert.Equal(open, npc.CurrentLocation);
    }
}
