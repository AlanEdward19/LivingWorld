using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using static LivingWorld.Tests.Unit.Players.PlayerAbsenceTests;

namespace LivingWorld.Tests.Unit.Players;

/// <summary>#227/#226: o corpo possuído que o motor guardou "em casa" sem cômodo (a origem do prédio, hoje um canto de piso: a parede é aresta) fica ali, com
/// caminho até a porta, e anda.</summary>
public class PlayerSettleTests
{
    private static readonly CellCoord[] Four = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    /// <summary>Um prédio posicionado e concluído e a célula de canto do térreo dele (a de menor linha e coluna), em coordenadas do mundo.</summary>
    private static (Building House, CellCoord Wall) WallOf(WorldState w)
    {
        var house = w.Buildings.Where(b => b.Position is not null && b.CompletedAtTick <= w.CurrentDate.TotalHours).OrderBy(b => b.Id.Value).First();
        var origin = house.Position!.Value;
        var wall = BuildingFootprintGenerator.Generate(house)
            .OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X).First().Cell;
        return (house, new CellCoord(origin.X + wall.X, origin.Y + wall.Y));
    }

    /// <summary>O caminho (passos de 4 vizinhas, só os que o motor aceita) de <paramref name="from"/> até <paramref name="goal"/>; vazio se já está lá.</summary>
    internal static List<CellCoord> PathTo(LivingWorld.Simulation.Core.WorldState w, Npc npc, CellCoord from, CellCoord goal)
    {
        var before = new Dictionary<CellCoord, CellCoord>();
        var queue = new Queue<CellCoord>([from]);
        var seen = new HashSet<CellCoord> { from };
        while (queue.Count > 0 && !seen.Contains(goal))
        {
            var cell = queue.Dequeue();
            foreach (var d in Four)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (seen.Contains(next) || !PlayerGround.Step(w, npc, cell, next).IsSuccess) continue;
                seen.Add(next);
                before[next] = cell;
                queue.Enqueue(next);
            }
        }

        Assert.Contains(goal, seen);
        var path = new List<CellCoord>();
        for (var at = goal; at != from; at = before[at]) path.Add(at);
        path.Reverse();
        return path;
    }

    private static (Arm Arm, Npc Npc, PlayerService Service, Building House) InAWall(ulong seed)
    {
        var arm = PlayerAbsenceTests.Village(seed, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        var service = new PlayerService(arm.Sink);
        var (house, wall) = WallOf(arm.World);
        npc.ExitBuilding();
        npc.MoveTo(wall, arm.World.CurrentDate.TotalHours); // o "em casa" sem Interior: a pose é a célula sólida
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan").IsSuccess);
        return (arm, npc, service, house);
    }

    [Fact]
    public void Possessing_a_body_in_a_corner_cell_keeps_it_on_walkable_floor_with_a_way_to_the_door()
    {
        var (arm, npc, _, house) = InAWall(1);
        var w = arm.World;
        var pose = PlayerGround.PoseOf(w, npc);
        Assert.True(PlayerGround.Walkable(w, pose));
        Assert.Equal(pose, npc.CurrentLocation); // não há parede onde o corpo ficou preso: ele fica onde estava
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
                if (PlayerGround.At(w, next) is { } s && s.Building.Id == house.Id && PlayerGround.Step(w, npc, cell, next).IsSuccess && seen.Add(next)) // #226: passo pelas arestas, não só pelas células
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
        foreach (var step in PathTo(w, npc, PlayerGround.PoseOf(w, npc), door)) // #226: o caminho contorna as paredes de dentro, pelas portas de dentro
        {
            var from = PlayerGround.PoseOf(w, npc);
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

    /// <summary>#237: um corpo no posto de trabalho do campo (prédio guardado, célula local fora da planta) de um prédio posicionado.</summary>
    private static (Arm Arm, Npc Npc, PlayerService Service, Building House, CellCoord Field) AtTheField(ulong seed)
    {
        var arm = PlayerAbsenceTests.Village(seed, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        var service = new PlayerService(arm.Sink);
        var (house, _) = WallOf(arm.World);
        var origin = house.Position!.Value;
        var footprint = BuildingFootprintGenerator.Generate(house).Select(c => c.Cell).ToHashSet();
        var maxX = footprint.Max(c => c.X);
        var field = new CellCoord(origin.X + maxX + 3, origin.Y); // chão aberto, 3 células depois da planta
        Assert.Null(PlayerGround.At(arm.World, field));
        npc.MoveTo(field, arm.World.CurrentDate.TotalHours);
        Assert.True(LivingWorld.Simulation.Cities.InteriorEntry.TryStand(arm.World, npc, house.Id, field).IsSuccess);
        return (arm, npc, service, house, field);
    }

    [Fact]
    public void Possessing_a_body_at_a_field_work_stand_leaves_it_in_the_open_with_no_building_in_the_pose_and_it_walks()
    {
        var (arm, npc, service, _, field) = AtTheField(1);
        var w = arm.World;
        Assert.NotNull(npc.Interior); // o prédio guardado com a célula fora da planta: a fórmula antiga da pose (Interior?.Building) dizia "dentro"
        Assert.Null(PlayerGround.InsideBuilding(w, npc)); // a pose já não diz "dentro"
        Assert.True(service.Incarnate(w, npc.Id, "alan").IsSuccess);
        Assert.Null(npc.Interior);
        Assert.Null(PlayerGround.InsideBuilding(w, npc));
        Assert.Equal(field, PlayerGround.PoseOf(w, npc));
        Assert.True(service.Move(w, "alan", 0, 1).IsSuccess); // o passo é aceito
    }

    [Fact]
    public void Possessing_a_body_inside_the_house_or_on_its_door_keeps_the_building_in_the_pose()
    {
        var arm = PlayerAbsenceTests.Village(1, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        var (house, _) = WallOf(arm.World);
        var origin = house.Position!.Value;
        var (door, doorstep) = PlayerGround.DoorOf(house)!.Value;
        var floor = BuildingFootprintGenerator.Generate(house).First(c => InteriorWalkability.IsWalkable(c.Material)).Cell;
        foreach (var local in new[] { floor, new CellCoord(door.X - origin.X, door.Y - origin.Y) })
        {
            npc.ExitBuilding();
            npc.MoveTo(new CellCoord(origin.X + local.X, origin.Y + local.Y), arm.World.CurrentDate.TotalHours);
            Assert.True(LivingWorld.Simulation.Cities.InteriorEntry.TryEnter(arm.World, npc, house.Id, new FloorLevel(0), local).IsSuccess);
            Assert.Equal(house.Id.Value, PlayerGround.InsideBuilding(arm.World, npc));
            var service = new PlayerService(arm.Sink);
            Assert.True(service.Incarnate(arm.World, npc.Id, "alan").IsSuccess);
            Assert.Equal(house.Id.Value, PlayerGround.InsideBuilding(arm.World, npc));
            Assert.True(PlayerGround.Walkable(arm.World, PlayerGround.PoseOf(arm.World, npc)));
            Assert.True(service.Logout(arm.World, "alan").IsSuccess);
        }
    }

    [Fact]
    public void Possessing_a_body_on_the_doorstep_is_outside_and_walks()
    {
        var arm = PlayerAbsenceTests.Village(1, PlayerControlTests.NoPace);
        var npc = Pick(arm.World);
        var (house, _) = WallOf(arm.World);
        npc.ExitBuilding();
        npc.MoveTo(PlayerGround.DoorOf(house)!.Value.Doorstep, arm.World.CurrentDate.TotalHours);
        var service = new PlayerService(arm.Sink);
        Assert.True(service.Incarnate(arm.World, npc.Id, "alan").IsSuccess);
        Assert.Null(PlayerGround.InsideBuilding(arm.World, npc));
        var from = PlayerGround.PoseOf(arm.World, npc);
        var open = new[] { new CellCoord(0, 1), new CellCoord(0, -1), new CellCoord(1, 0), new CellCoord(-1, 0) }
            .First(d => PlayerGround.Step(arm.World, npc, from, new CellCoord(from.X + d.X, from.Y + d.Y)).IsSuccess);
        Assert.True(service.Move(arm.World, "alan", open.X, open.Y).IsSuccess);
    }
}
