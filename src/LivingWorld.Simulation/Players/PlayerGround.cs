using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>AD-197d/197j: o chão que o corpo conduzido pisa, lido do mundo e nunca guardado. A planta de cada prédio concluído e
/// posicionado fica na grade do mundo (a mesma das rotas, <c>StreetRoutes</c>), andar por andar (<c>BuildingLayoutCache</c>): parede
/// não se atravessa, piso, porta e escada sim; entre fora e dentro só pela porta, vinda da soleira; nos andares de cima só dentro do
/// prédio, e de um andar a outro só pela escada; quem está na célula (pela pose, no mesmo andar) ocupa a célula. Só lê.</summary>
public static class PlayerGround
{
    /// <summary>O prédio cuja planta cobre a célula, com o material do andar ali e a porta e a soleira dele (coordenadas do mundo).</summary>
    public readonly record struct Spot(Building Building, BuildingMaterial Material, CellCoord Door, CellCoord Doorstep);

    /// <summary>O prédio concluído que cobre <paramref name="cell"/> no andar <paramref name="floor"/>, ou nulo (chão aberto, ou o
    /// prédio não tem esse andar).</summary>
    public static Spot? At(WorldState world, CellCoord cell, int floor = 0)
    {
        long now = world.CurrentDate.TotalHours;
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } origin || building.CompletedAtTick > now) continue;
            var local = new CellCoord(cell.X - origin.X, cell.Y - origin.Y);
            var footprint = BuildingFootprintGenerator.Generate(building);
            bool covers = false;
            for (int i = 0; i < footprint.Count && !covers; i++) covers = footprint[i].Cell == local;
            if (!covers) continue;
            var layouts = BuildingLayoutCache.Of(building).Layout.FloorLayouts;
            if (floor < 0 || floor >= layouts.Count) return null;
            var material = layouts[floor].Cells.First(c => c.Cell == local).Material;
            var (door, facing) = LotGeometry.DoorOf(footprint);
            var doorCell = new CellCoord(origin.X + door.X, origin.Y + door.Y);
            return new Spot(building, material, doorCell, new CellCoord(doorCell.X + facing.X, doorCell.Y + facing.Y));
        }
        return null;
    }

    /// <summary>A porta e a soleira (fora, à frente dela) de um prédio posicionado; nulo sem posição.</summary>
    public static (CellCoord Door, CellCoord Doorstep)? DoorOf(Building building)
    {
        if (building.Position is not { } origin) return null;
        var (door, facing) = LotGeometry.DoorOf(BuildingFootprintGenerator.Generate(building));
        var doorCell = new CellCoord(origin.X + door.X, origin.Y + door.Y);
        return (doorCell, new CellCoord(doorCell.X + facing.X, doorCell.Y + facing.Y));
    }

    /// <summary>O andar em que o NPC está (0 fora de prédio ou no térreo).</summary>
    public static int FloorOf(Npc npc) => npc.Interior?.Floor.Value ?? 0;

    /// <summary>Onde o NPC está agora, na grade do mundo: a célula do prédio em que o motor o pôs (em qualquer andar); em viagem no passo
    /// fino, o ponto da rota; senão o lugar dele.</summary>
    public static CellCoord PoseOf(WorldState world, Npc npc)
    {
        if (npc.Interior is { } inside && world.FindBuilding(inside.Building) is { Position: { } origin })
            return new CellCoord(origin.X + inside.LocalCell.X, origin.Y + inside.LocalCell.Y);
        return world.FineTime && npc.CurrentAction == ActionType.Travel
            ? TravelPose.CellAt(world, npc, world.CurrentDate.TotalMilliseconds)
            : npc.CurrentLocation;
    }

    /// <summary>#237: o prédio em cuja planta o corpo está de verdade: o do <see cref="Npc.Interior"/> se a célula local é da planta
    /// (qualquer célula nos andares de cima; no térreo, célula coberta pela planta), ou o que cobre a célula onde ele está no chão.
    /// O posto de trabalho no campo (<see cref="InteriorEntry.TryStand"/>) guarda o prédio com célula local FORA da planta: ali o corpo
    /// está na rua, não dentro. Nulo na rua.</summary>
    public static long? InsideBuilding(WorldState world, Npc npc)
    {
        if (npc.Interior is { } inside)
        {
            if (world.FindBuilding(inside.Building) is { Position: not null } building
                && (inside.Floor.Value > 0 || BuildingLayoutCache.Of(building).CoversGround(inside.LocalCell)))
                return inside.Building.Value;
            return null;
        }

        return At(world, npc.CurrentLocation)?.Building.Id.Value;
    }

    /// <summary>Os dois estão no mesmo espaço: o mesmo andar e, acima do térreo, o mesmo prédio.</summary>
    public static bool SameLevel(Npc a, Npc b) =>
        FloorOf(a) == FloorOf(b) && (FloorOf(a) == 0 || a.Interior!.Building == b.Interior!.Building);

    /// <summary>Quem (vivo, que não seja <paramref name="self"/>) está em <paramref name="cell"/> no andar de <paramref name="self"/>
    /// agora; nulo se ninguém. Na mesma cidade de quem anda.</summary>
    public static Npc? PersonAt(WorldState world, Npc self, CellCoord cell) => PersonAt(world, self, cell, FloorOf(self), self.Interior?.Building);

    /// <summary>Quem está em <paramref name="cell"/> no andar <paramref name="floor"/> (do prédio <paramref name="building"/>, acima do térreo).</summary>
    public static Npc? PersonAt(WorldState world, Npc self, CellCoord cell, int floor, BuildingId? building)
    {
        CrowdAt(world, self, cell, floor, building, out var found);
        return found;
    }

    /// <summary>AD-197u: quantas pessoas (vivas, que não sejam <paramref name="self"/>) estão em <paramref name="cell"/> no andar
    /// <paramref name="floor"/>, e a de menor id entre elas (<paramref name="first"/>). Uma só passada pelos vivos.</summary>
    public static int CrowdAt(WorldState world, Npc self, CellCoord cell, int floor, BuildingId? building, out Npc? first)
    {
        first = null;
        int count = 0;
        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.Id == self.Id || other.City != self.City || FloorOf(other) != floor) continue;
            if (floor > 0 && other.Interior!.Building != building) continue;
            if (other.CurrentAction != ActionType.Travel && other.Interior is null && other.CurrentLocation != cell)
                continue; // parado em outra célula: não precisa de pose
            if (PoseOf(world, other) != cell) continue;
            count++;
            if (first is null || other.Id.Value < first.Id.Value) first = other;
        }
        return count;
    }

    /// <summary>Dá para pisar em <paramref name="cell"/> no térreo? Dentro do mapa; fora de prédio, sim; dentro, se ali não é parede.</summary>
    public static bool Walkable(WorldState world, CellCoord cell) =>
        world.Map.TryGetCell(cell, out _) && (At(world, cell) is not { } spot || InteriorWalkability.IsWalkable(spot.Material));

    /// <summary>O passo de <paramref name="from"/> a <paramref name="to"/> (vizinhas) no andar do corpo passa pelas paredes? No térreo,
    /// entre fora e dentro só pela porta (da soleira para a porta e de volta) e entre dois prédios nunca; acima do térreo, só dentro do
    /// mesmo prédio; na diagonal, sem cortar o canto de uma parede; sem gente na célula.</summary>
    public static Result<Unit> Step(WorldState world, Npc npc, CellCoord from, CellCoord to)
    {
        if (!world.Map.TryGetCell(to, out _)) return Result<Unit>.Fail("move: fora do mapa");
        int floor = FloorOf(npc);
        var here = At(world, from, floor);
        var there = At(world, to, floor);
        if (there is { } t && !InteriorWalkability.IsWalkable(t.Material)) return Result<Unit>.Fail("move: parede");
        long hereId = here?.Building.Id.Value ?? -1, thereId = there?.Building.Id.Value ?? -1;
        if (floor > 0 && (there is null || thereId != npc.Interior!.Building.Value))
            return Result<Unit>.Fail("move: parede (no andar de cima só se anda dentro do prédio)");
        if (hereId != thereId)
        {
            bool entering = here is null && there is { } inn && to == inn.Door && from == inn.Doorstep;
            bool leaving = there is null && here is { } outn && from == outn.Door && to == outn.Doorstep;
            if (!entering && !leaving) return Result<Unit>.Fail("move: parede (entra e sai só pela porta)");
        }
        else if (from.X != to.X && from.Y != to.Y
                 && (!Open(world, new CellCoord(to.X, from.Y), thereId, floor) || !Open(world, new CellCoord(from.X, to.Y), thereId, floor)))
            return Result<Unit>.Fail("move: canto de parede");
        // AD-197u: a célula (1,5 m, 2,25 m²) comporta gente; só barra quando já está cheia (PlayerRules.CellCrowd)
        int crowd = CrowdAt(world, npc, to, floor, npc.Interior?.Building, out var someone);
        if (crowd > 0 && crowd >= world.Players.CellCrowd) return Result<Unit>.Fail($"move: {someone!.Name} está ali");
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>A célula do canto da diagonal está aberta no mesmo espaço (fora, ou no mesmo prédio e andar sem parede)?</summary>
    private static bool Open(WorldState world, CellCoord cell, long buildingId, int floor)
    {
        if (!world.Map.TryGetCell(cell, out _)) return false;
        var spot = At(world, cell, floor);
        return (spot?.Building.Id.Value ?? -1) == buildingId && (spot is not { } s || InteriorWalkability.IsWalkable(s.Material));
    }
}
