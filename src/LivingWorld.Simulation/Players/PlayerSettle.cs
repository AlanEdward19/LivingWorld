using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#227: o corpo que o jogador passa a conduzir não nasce preso na parede. "Em casa" sem cômodo o motor guarda o NPC na origem
/// do prédio (<c>CurrentLocation</c>), e a origem da planta é parede em alguns prédios: <see cref="PlayerGround.Step"/> barra todo passo
/// dali. Ao incarnar, quem está numa célula sólida de prédio vai, sem RNG, para a célula de piso mais próxima dela que tem caminho até a
/// porta; sem piso, para a soleira (rua em frente à porta). Só o corpo conduzido; o mundo de quem não é jogador não muda.</summary>
internal static class PlayerSettle
{
    private static readonly CellCoord[] Around = [new(0, -1), new(-1, 0), new(1, 0), new(0, 1)];

    /// <summary>Põe o corpo em chão andável se a pose dele é célula sólida do térreo de um prédio; devolve se mexeu.</summary>
    public static bool OutOfTheWall(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Interior is not null) return LeaveTheField(world, ctx, npc); // a entrada no prédio já validou a célula contra a planta
        var pose = PlayerGround.PoseOf(world, npc);
        if (PlayerGround.At(world, pose) is not { } spot || InteriorWalkability.IsWalkable(spot.Material)) return false;
        var origin = spot.Building.Position!.Value;
        var target = NearestFloor(spot.Building, origin, new CellCoord(pose.X - origin.X, pose.Y - origin.Y)) ?? spot.Doorstep;
        npc.MoveTo(target, ctx.CurrentTick);
        if (npc.Fine is not null) npc.SetFine(null); // a posição contínua partiria da parede de novo
        return true;
    }

    /// <summary>#237: o posto de trabalho no campo guarda o prédio com a célula local fora da planta (a pose é uma célula do chão aberto).
    /// Para conduzir, o corpo é de quem está na rua: vai para a célula da pose (o campo) e sai do prédio, em vez de o app entrar numa
    /// casa em que ele não está. Dentro da planta (ou nos andares de cima) nada muda.</summary>
    private static bool LeaveTheField(WorldState world, TickContext ctx, Npc npc)
    {
        if (PlayerGround.InsideBuilding(world, npc) is not null) return false;
        var pose = PlayerGround.PoseOf(world, npc);
        npc.ExitBuilding();
        if (npc.CurrentLocation != pose) npc.MoveTo(pose, ctx.CurrentTick);
        if (npc.Fine is not null) npc.SetFine(null);
        return true;
    }

    /// <summary>A célula de piso do térreo (coordenadas do mundo) ligada à porta por piso (vizinhança de 4) mais perto de
    /// <paramref name="from"/> (local); empate pela menor linha e coluna. Nulo se a planta não tem piso além da porta.</summary>
    private static CellCoord? NearestFloor(Building building, CellCoord origin, CellCoord from)
    {
        var walkable = BuildingLayoutCache.Of(building).WalkableByFloor[0].Select(c => c.Cell).ToHashSet();
        var door = PlayerGround.DoorOf(building)!.Value.Door;
        var start = new CellCoord(door.X - origin.X, door.Y - origin.Y);
        var seen = new HashSet<CellCoord> { start };
        var queue = new Queue<CellCoord>([start]);
        CellCoord? best = null;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell != start && (best is null || Better(cell, best.Value, from))) best = cell;
            foreach (var d in Around)
            {
                var next = new CellCoord(cell.X + d.X, cell.Y + d.Y);
                if (walkable.Contains(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }

        return best is { } found ? new CellCoord(origin.X + found.X, origin.Y + found.Y) : null;
    }

    private static bool Better(CellCoord a, CellCoord b, CellCoord from)
    {
        int da = Dist(a, from), db = Dist(b, from);
        return da != db ? da < db : a.Y != b.Y ? a.Y < b.Y : a.X < b.X;
    }

    private static int Dist(CellCoord a, CellCoord b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
