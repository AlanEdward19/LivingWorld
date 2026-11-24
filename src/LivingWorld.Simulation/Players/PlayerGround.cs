using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>AD-197d: o chão que o corpo conduzido pisa, lido do mundo e nunca guardado. A planta de cada prédio concluído e posicionado
/// fica na grade do mundo (a mesma das rotas, <c>StreetRoutes</c>): parede não se atravessa, piso, porta e escada sim; entre fora e
/// dentro só pela porta, vinda da soleira; quem está na célula (pela pose: o viajante no ponto da rota em que está agora, quem está
/// num prédio na célula do térreo em que o motor o pôs) ocupa a célula. Só lê: a validação nunca muda o mundo.</summary>
public static class PlayerGround
{
    /// <summary>O prédio cuja planta cobre a célula, com o material do térreo ali e a porta e a soleira dele (coordenadas do mundo).</summary>
    public readonly record struct Spot(Building Building, BuildingMaterial Material, CellCoord Door, CellCoord Doorstep);

    /// <summary>O prédio concluído que cobre <paramref name="cell"/>, ou nulo (chão aberto).</summary>
    public static Spot? At(WorldState world, CellCoord cell)
    {
        long now = world.CurrentDate.TotalHours;
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } origin || building.CompletedAtTick > now) continue;
            int lx = cell.X - origin.X, ly = cell.Y - origin.Y;
            var footprint = BuildingFootprintGenerator.Generate(building);
            for (int i = 0; i < footprint.Count; i++)
            {
                if (footprint[i].Cell.X != lx || footprint[i].Cell.Y != ly) continue;
                var (door, facing) = LotGeometry.DoorOf(footprint);
                var doorCell = new CellCoord(origin.X + door.X, origin.Y + door.Y);
                return new Spot(building, footprint[i].Material, doorCell, new CellCoord(doorCell.X + facing.X, doorCell.Y + facing.Y));
            }
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

    /// <summary>Onde o NPC está agora: a célula do térreo do prédio em que o motor o pôs; em viagem no passo fino, o ponto da rota; senão
    /// o lugar dele.</summary>
    public static CellCoord PoseOf(WorldState world, Npc npc)
    {
        if (npc.Interior is { } inside && inside.Floor.Value == 0 && world.FindBuilding(inside.Building) is { Position: { } origin })
            return new CellCoord(origin.X + inside.LocalCell.X, origin.Y + inside.LocalCell.Y);
        if (npc.Interior is not null) return new CellCoord(int.MinValue, int.MinValue); // andar de cima: não ocupa o térreo
        return world.FineTime && npc.CurrentAction == ActionType.Travel
            ? TravelPose.CellAt(world, npc, world.CurrentDate.TotalMilliseconds)
            : npc.CurrentLocation;
    }

    /// <summary>Quem (vivo, que não seja <paramref name="self"/>) está em <paramref name="cell"/> agora; nulo se ninguém. Na mesma
    /// cidade de quem anda (ou sem cidade): quem está em outra cidade não chega a esta célula.</summary>
    public static Npc? PersonAt(WorldState world, Npc self, CellCoord cell)
    {
        Npc? found = null;
        foreach (var other in world.AliveNpcIndex.Alive)
        {
            if (other.Id == self.Id || other.City != self.City) continue;
            if (other.CurrentAction != ActionType.Travel && other.Interior is null
                && Math.Max(Math.Abs(other.CurrentLocation.X - cell.X), Math.Abs(other.CurrentLocation.Y - cell.Y)) > 0)
                continue; // parado longe: não precisa de pose
            if (PoseOf(world, other) != cell) continue;
            if (found is null || other.Id.Value < found.Id.Value) found = other;
        }
        return found;
    }

    /// <summary>Dá para pisar em <paramref name="cell"/>? Dentro do mapa; fora de prédio, sim; dentro, se o térreo ali não é parede.</summary>
    public static bool Walkable(WorldState world, CellCoord cell) =>
        world.Map.TryGetCell(cell, out _) && (At(world, cell) is not { } spot || InteriorWalkability.IsWalkable(spot.Material));

    /// <summary>O passo de <paramref name="from"/> a <paramref name="to"/> (vizinhas) passa pelas paredes? Entre fora e dentro só pela
    /// porta (da soleira para a porta e de volta); entre dois prédios, nunca; na diagonal, sem cortar o canto de uma parede.</summary>
    public static Result<Unit> Step(WorldState world, Npc npc, CellCoord from, CellCoord to)
    {
        if (!world.Map.TryGetCell(to, out _)) return Result<Unit>.Fail("move: fora do mapa");
        var here = At(world, from);
        var there = At(world, to);
        if (there is { } t && !InteriorWalkability.IsWalkable(t.Material)) return Result<Unit>.Fail("move: parede");
        long hereId = here?.Building.Id.Value ?? -1, thereId = there?.Building.Id.Value ?? -1;
        if (hereId != thereId)
        {
            bool entering = here is null && there is { } inn && to == inn.Door && from == inn.Doorstep;
            bool leaving = there is null && here is { } outn && from == outn.Door && to == outn.Doorstep;
            if (!entering && !leaving) return Result<Unit>.Fail("move: parede (entra e sai só pela porta)");
        }
        else if (from.X != to.X && from.Y != to.Y
                 && (!Open(world, new CellCoord(to.X, from.Y), thereId) || !Open(world, new CellCoord(from.X, to.Y), thereId)))
            return Result<Unit>.Fail("move: canto de parede");
        if (PersonAt(world, npc, to) is { } someone) return Result<Unit>.Fail($"move: {someone.Name} está ali");
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>A célula do canto da diagonal está aberta no mesmo espaço (fora, ou no mesmo prédio sem parede)?</summary>
    private static bool Open(WorldState world, CellCoord cell, long buildingId)
    {
        if (!world.Map.TryGetCell(cell, out _)) return false;
        var spot = At(world, cell);
        return (spot?.Building.Id.Value ?? -1) == buildingId && (spot is not { } s || InteriorWalkability.IsWalkable(s.Material));
    }
}
