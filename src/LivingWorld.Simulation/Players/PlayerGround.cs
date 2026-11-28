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
    public readonly record struct Spot(Building Building, BuildingMaterial Material, CellCoord Door, CellCoord Doorstep, CellCoord Origin, IReadOnlySet<WallEdge> Walls)
    {
        /// <summary>#226: a aresta entre as células <paramref name="a"/> e <paramref name="b"/> (do mundo, vizinhas) é parede deste prédio neste andar (de fora ou de cômodo)?</summary>
        public bool Closed(CellCoord a, CellCoord b) =>
            Walls.Contains(WallEdge.Between(new CellCoord(a.X - Origin.X, a.Y - Origin.Y), new CellCoord(b.X - Origin.X, b.Y - Origin.Y)));
    }

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
            return new Spot(building, material, doorCell, new CellCoord(doorCell.X + facing.X, doorCell.Y + facing.Y), origin,
                BuildingRooms.Of(building, world.ObjectCatalog).Blocked[floor]);
        }
        return null;
    }

    /// <summary>#255: o passo de <paramref name="from"/> a <paramref name="to"/> é debaixo de teto: as duas células são da planta do MESMO prédio (o piso, a
    /// porta). Neve e lama não alongam esse passo ("Chão firme −78%" dentro de casa no inverno).</summary>
    public static bool Sheltered(WorldState world, CellCoord from, CellCoord to) =>
        world.Buildings.Count > 0 && At(world, from) is { } a && At(world, to) is { } b && a.Building.Id == b.Building.Id;

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
        int floor = FloorOf(npc);
        if (TerrainRefusal(world, floor, npc.Interior?.Building, from, to, cell => At(world, cell, floor)) is { } refusal)
            return Result<Unit>.Fail(refusal);
        // AD-197u: a célula (1,5 m, 2,25 m²) comporta gente; só barra quando já está cheia (PlayerRules.CellCrowd)
        int crowd = CrowdAt(world, npc, to, floor, npc.Interior?.Building, out var someone);
        if (crowd > 0 && crowd >= world.Players.CellCrowd) return Result<Unit>.Fail($"move: {someone!.Name} está ali");
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>#255: o motivo de o corpo não poder dar o passo de <paramref name="from"/> a <paramref name="to"/> pela regra do chão (parede, fora do mapa), ou nulo.</summary>
    internal static string? StepRefusal(WorldState world, Npc npc, CellCoord from, CellCoord to)
    {
        int floor = FloorOf(npc);
        return TerrainRefusal(world, floor, npc.Interior?.Building, from, to, cell => At(world, cell, floor));
    }

    /// <summary>#255: a vizinha de <paramref name="from"/> aonde o corpo pode ir e que fica mais perto do rumo de <paramref name="toward"/> (empate: ordem fixa); nula se nenhuma.</summary>
    internal static CellCoord? OpenNeighbour(WorldState world, Npc npc, CellCoord from, CellCoord toward)
    {
        int floor = FloorOf(npc);
        var cache = new Dictionary<CellCoord, Spot?>();
        Spot? Lookup(CellCoord c)
        {
            if (!cache.TryGetValue(c, out var spot)) cache[c] = spot = At(world, c, floor);
            return spot;
        }

        CellCoord? best = null;
        long bestDistance = long.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var next = new CellCoord(from.X + dx, from.Y + dy);
                if (TerrainRefusal(world, floor, npc.Interior?.Building, from, next, Lookup) is not null) continue;
                long d = (long)(next.X - toward.X) * (next.X - toward.X) + (long)(next.Y - toward.Y) * (next.Y - toward.Y);
                if (d < bestDistance) { best = next; bestDistance = d; }
            }

        return best;
    }

    /// <summary>#248: a regra do chão (sem gente) do passo de <paramref name="from"/> a <paramref name="to"/> no andar
    /// <paramref name="floor"/>; o motivo da recusa ou nulo. <paramref name="at"/> diz o prédio em cada célula nesse andar: é a MESMA regra
    /// do passo do corpo e da máscara que o cliente consulta (<see cref="Mask"/>), então o cliente nunca prevê com regra própria.</summary>
    internal static string? TerrainRefusal(WorldState world, int floor, BuildingId? ownBuilding, CellCoord from, CellCoord to, Func<CellCoord, Spot?> at)
    {
        if (!world.Map.TryGetCell(to, out _)) return "move: fora do mapa";
        var here = at(from);
        var there = at(to);
        long hereId = here?.Building.Id.Value ?? -1, thereId = there?.Building.Id.Value ?? -1;
        if (floor > 0 && (there is null || thereId != ownBuilding?.Value))
            return "move: parede (no andar de cima só se anda dentro do prédio)";
        if (hereId != thereId)
        {
            // #226: a parede é aresta: de fora para dentro só pela abertura da porta (da soleira para a célula da porta, em linha reta)
            bool entering = here is null && there is { } inn && to == inn.Door && from == inn.Doorstep;
            bool leaving = there is null && here is { } outn && from == outn.Door && to == outn.Doorstep;
            if (!entering && !leaving) return "move: parede (entra e sai só pela porta)";
        }
        else if (from.X != to.X && from.Y != to.Y)
        {
            // diagonal: as duas vias (por cada canto) precisam estar abertas, sem cortar a ponta de uma parede
            var c1 = new CellCoord(to.X, from.Y);
            var c2 = new CellCoord(from.X, to.Y);
            if (!Open(world, c1, thereId, at) || !Open(world, c2, thereId, at)
                || (there is { } t && (t.Closed(from, c1) || t.Closed(c1, to) || t.Closed(from, c2) || t.Closed(c2, to))))
                return "move: canto de parede";
        }
        else if (there is { } wall && wall.Closed(from, to)) return "move: parede";
        return null;
    }

    /// <summary>#248: a máscara de passos ao redor de <paramref name="center"/> (janela de lado 2·<paramref name="radius"/>+1, linha a linha a partir
    /// do canto de cima à esquerda): para cada célula, um bit por vizinha (<c>(dy+1)·3+(dx+1)</c>, sem o do meio) dizendo se o passo é permitido
    /// pela regra do chão (<see cref="TerrainRefusal"/>); a gente na célula é regra à parte (<see cref="PlayerRules.CellCrowd"/>). Uma só
    /// leitura dos prédios da janela, não uma por passo.</summary>
    public static int[] Mask(WorldState world, int floor, BuildingId? ownBuilding, CellCoord center, int radius)
    {
        int x0 = center.X - radius - 1, y0 = center.Y - radius - 1, side = 2 * radius + 3;
        var spots = new Dictionary<CellCoord, Spot?>();
        long now = world.CurrentDate.TotalHours;
        foreach (var building in world.Buildings)
        {
            if (building.Position is not { } origin || building.CompletedAtTick > now) continue;
            if (origin.X > x0 + side + 64 || origin.Y > y0 + side + 64 || origin.X < x0 - 64 || origin.Y < y0 - 64) continue; // longe demais para cobrir a janela
            var footprint = BuildingFootprintGenerator.Generate(building);
            var layouts = BuildingLayoutCache.Of(building).Layout.FloorLayouts;
            if (floor < 0 || floor >= layouts.Count) continue;
            var (door, facing) = LotGeometry.DoorOf(footprint);
            var doorCell = new CellCoord(origin.X + door.X, origin.Y + door.Y);
            var doorstep = new CellCoord(doorCell.X + facing.X, doorCell.Y + facing.Y);
            IReadOnlySet<WallEdge>? walls = null;
            foreach (var cell in layouts[floor].Cells)
            {
                var world0 = new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y);
                if (world0.X >= x0 && world0.X < x0 + side && world0.Y >= y0 && world0.Y < y0 + side)
                    spots[world0] = new Spot(building, cell.Material, doorCell, doorstep, origin,
                        walls ??= BuildingRooms.Of(building, world.ObjectCatalog).Blocked[floor]);
            }
        }
        Spot? Lookup(CellCoord c) => spots.TryGetValue(c, out var spot) ? spot : null;
        int inner = 2 * radius + 1;
        var mask = new int[inner * inner];
        for (int j = 0; j < inner; j++)
            for (int i = 0; i < inner; i++)
            {
                var from = new CellCoord(x0 + 1 + i, y0 + 1 + j);
                int bits = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        if (TerrainRefusal(world, floor, ownBuilding, from, new CellCoord(from.X + dx, from.Y + dy), Lookup) is null) bits |= 1 << ((dy + 1) * 3 + (dx + 1));
                    }
                mask[j * inner + i] = bits;
            }
        return mask;
    }

    /// <summary>A célula do canto da diagonal está aberta no mesmo espaço (fora, ou no mesmo prédio e andar sem parede)?</summary>
    private static bool Open(WorldState world, CellCoord cell, long buildingId, Func<CellCoord, Spot?> at)
    {
        if (!world.Map.TryGetCell(cell, out _)) return false;
        var spot = at(cell);
        return (spot?.Building.Id.Value ?? -1) == buildingId;
    }
}
