using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Objects;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities;

/// <summary>Coloca o NPC dentro do prédio quando a ação e o lugar pedem: dormindo na célula do próprio
/// household (<see cref="Household.Dwelling"/>) ou trabalhando na célula do seu <see cref="Workplace"/>
/// (<see cref="Workplace.Building"/>). Fora dessas duas condições o NPC fica fora. Andar e célula são função
/// pura de <c>(NpcId, BuildingId)</c>, sem <c>Random</c>. Só lê a decisão já tomada; nunca move o NPC.</summary>
[LivingCapability("BUILDING_OCCUPANCY", Consumers = ["interior.occupants"])]
[RunsAfter(typeof(BehaviorDecisionSystem))]
public sealed class BuildingOccupancySystem : ISimulationSystem
{
    public const string SystemName = "building-occupancy";

    private readonly Dictionary<CellCoord, int> _crowd = []; // reaproveitado a cada tick (a simulação é sequencial)

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var npcs = world.Npcs;
        Dictionary<CellCoord, int>? crowd = null; // quantos em cada célula do chão (mundo); só montado se alguém entra para trabalhar
        for (int i = 0; i < npcs.Count; i++)
        {
            var npc = npcs[i];
            // Só dormir e trabalhar põem alguém dentro: quem está fora fazendo outra coisa não tem o que resolver.
            if (npc.Interior is null && npc.CurrentAction is not (ActionType.Sleep or ActionType.Work)) continue;
            // AD-197j: o jogador que subiu a escada fica no andar em que andou (só sai pelos próprios pés)
            if (npc.ControllerId is not null && npc.ControlledAction == ActionType.Idle && npc.Interior is { Floor.Value: > 0 }) continue;

            var target = TargetBuilding(world, npc);
            if (target is null)
            {
                if (npc.Interior is not null)
                {
                    Release(world, npc, crowd);
                    npc.ExitBuilding();
                }

                continue;
            }

            // Andar e célula são puros em (NpcId, BuildingId): quem já está no prédio certo não muda de lugar.
            if (npc.Interior?.Building == target.Value.Building.Id) continue;
            if (target.Value.Workplace is { } workplace && target.Value.Building.Position is not null)
            {
                // #223: o posto vem do ofício e do lugar, com lotação por célula
                crowd ??= CrowdOnGround(world);
                Release(world, npc, crowd);
                var cells = WorkStands.StandsOf(world, target.Value.Building, workplace);
                if (WorkStands.Pick(world, cells, crowd) is { } stand && InteriorEntry.TryStand(world, npc, target.Value.Building.Id, stand).IsSuccess)
                    crowd[stand] = crowd.GetValueOrDefault(stand) + 1;
                else if (npc.Interior is not null)
                    npc.ExitBuilding(); // sem vaga em nenhum posto: espera fora, não se empilha
                continue;
            }

            Release(world, npc, crowd);
            var (building, floor, cell) = Place(npc.Id, target.Value.Building, target.Value.GroundOnly, world.ObjectCatalog, SleeperRank(world, npc, target.Value.GroundOnly));
            if (InteriorEntry.TryEnter(world, npc, building, floor, cell).IsSuccess && floor.Value == 0 && crowd is not null
                && building.Value == target.Value.Building.Id.Value && target.Value.Building.Position is { } origin)
            {
                var at = new CellCoord(origin.X + cell.X, origin.Y + cell.Y);
                crowd[at] = crowd.GetValueOrDefault(at) + 1;
            }
        }
    }

    /// <summary>Quantos NPCs vivos estão em cada célula do térreo (a pose dentro de prédio, em coordenadas do mundo).</summary>
    private Dictionary<CellCoord, int> CrowdOnGround(WorldState world)
    {
        var crowd = _crowd;
        crowd.Clear();
        var npcs = world.Npcs;
        for (int i = 0; i < npcs.Count; i++)
            if (npcs[i] is { IsAlive: true, Interior: { Floor.Value: 0 } inside } npc && world.FindBuilding(inside.Building) is { Position: { } origin })
            {
                var at = new CellCoord(origin.X + inside.LocalCell.X, origin.Y + inside.LocalCell.Y);
                crowd[at] = crowd.GetValueOrDefault(at) + 1;
            }

        return crowd;
    }

    private static void Release(WorldState world, Npc npc, Dictionary<CellCoord, int>? crowd)
    {
        if (crowd is null || npc.Interior is not { Floor.Value: 0 } inside || world.FindBuilding(inside.Building) is not { Position: { } origin }) return;
        var at = new CellCoord(origin.X + inside.LocalCell.X, origin.Y + inside.LocalCell.Y);
        if (crowd.TryGetValue(at, out int count)) crowd[at] = Math.Max(0, count - 1);
    }

    private static (Building Building, bool GroundOnly, Workplace? Workplace)? TargetBuilding(WorldState world, Npc npc)
    {
        if (!npc.IsAlive) return null;

        BuildingId? target = null;
        bool ground = false;
        Workplace? work = null;
        switch (npc.CurrentAction)
        {
            case ActionType.Sleep:
                if (npc.Household is { } householdId && world.FindHousehold(householdId) is { Dwelling: { } dwelling } household
                    && npc.CurrentLocation == household.Location)
                    target = dwelling;
                break;
            case ActionType.Work:
                ground = true;
                if (npc.Employer is { } employerId && world.FindWorkplace(employerId) is { Building: { } workplaceBuilding } workplace
                    && npc.CurrentLocation == workplace.Location)
                {
                    target = workplaceBuilding;
                    work = workplace;
                }
                break;
        }

        if (target is not { } buildingId || world.FindBuilding(buildingId) is not { } building) return null;
        return (building, ground, work);
    }

    /// <summary>Andar (térreo quando <paramref name="groundOnly"/>) e célula caminhável, escolhidos só por hash
    /// estável de <c>(NpcId, BuildingId)</c>. #237: quem vai dormir (não <paramref name="groundOnly"/>) deita numa cama que o motor pôs na planta
    /// (<see cref="HomeFurniture"/>) ou, sem cama, em piso livre; nunca na porta nem na escada se houver piso (o corpo deitado atravessava a parede).</summary>
    internal static (BuildingId Building, FloorLevel Floor, CellCoord Cell) Place(NpcId npc, Building building, bool groundOnly, LivingWorld.Domain.Objects.ObjectCatalog? catalog = null,
        int? rank = null)
    {
        ulong hash = StableHash.Mix(npc.Value ^ (building.Id.Value << 20));
        if (!groundOnly && RestingSpots(building, catalog) is { Count: > 0 } spots)
        {
            // #242: cada morador da casa tem o seu lugar (a cama, depois o piso livre), em vez de todos no mesmo por sorteio de hash
            var spot = rank is { } r ? SpotForRank(building, catalog, spots, r) : spots[(int)(StableHash.Mix((long)hash) % (ulong)spots.Count)];
            return (building.Id, new FloorLevel(spot.Floor), spot.Cell);
        }

        var walkableByFloor = BuildingLayoutCache.Of(building).WalkableByFloor;
        int floorIndex = groundOnly ? 0 : (int)(hash % (ulong)walkableByFloor.Length);
        var walkable = walkableByFloor[floorIndex];
        var chosen = walkable[(int)(StableHash.Mix((long)hash) % (ulong)walkable.Length)];
        return (building.Id, new FloorLevel(floorIndex), chosen.Cell);
    }

    /// <summary>#242: a posição do NPC entre os moradores da casa (por id), a base de um lugar para cada um; nulo fora de casa ou no trabalho.</summary>
    private static int? SleeperRank(WorldState world, Npc npc, bool groundOnly)
    {
        if (groundOnly || npc.Household is not { } id || world.FindHousehold(id) is not { } household) return null;
        int rank = 0;
        var members = household.Members;
        for (int i = 0; i < members.Count; i++)
            if (members[i].Value < npc.Id.Value) rank++;
        return rank;
    }

    /// <summary>O lugar do <paramref name="rank"/>-ésimo morador: as camas primeiro; passadas as camas, o piso livre da casa (distinto
    /// enquanto houver célula); sem piso livre, volta ao começo da lista.</summary>
    private static FurnitureSpot SpotForRank(Building building, LivingWorld.Domain.Objects.ObjectCatalog? catalog, List<FurnitureSpot> spots, int rank)
    {
        if (rank < spots.Count) return spots[rank];
        var overflow = OverflowSpots(building, catalog);
        return overflow.Count > 0 ? overflow[(rank - spots.Count) % overflow.Count] : spots[rank % spots.Count];
    }

    /// <summary>Piso livre (fora da mobília, da porta e da escada) além das camas, na ordem da planta.</summary>
    private static List<FurnitureSpot> OverflowSpots(Building building, LivingWorld.Domain.Objects.ObjectCatalog? catalog)
    {
        var cached = BuildingLayoutCache.Of(building);
        if (OverflowCache.TryGetValue(building, out var known) && ReferenceEquals(known.Layout, cached) && ReferenceEquals(known.Catalog, catalog)) return known.Spots;
        var spots = ComputeOverflowSpots(building, catalog);
        OverflowCache.AddOrUpdate(building, new RestPlan(cached, catalog, spots));
        return spots;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Building, RestPlan> OverflowCache = new();

    private static List<FurnitureSpot> ComputeOverflowSpots(Building building, LivingWorld.Domain.Objects.ObjectCatalog? catalog)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var furniture = catalog is null ? [] : HomeFurniture.Of(building, catalog);
        var taken = furniture.Where(p => p.Spot is not null).Select(p => (p.Spot!.Value.Floor, p.Spot!.Value.Cell)).ToHashSet();
        var free = new List<FurnitureSpot>();
        foreach (var floor in layout.FloorLayouts)
        {
            var materials = floor.Cells.ToDictionary(c => c.Cell, c => c.Material);
            var start = FurniturePlanner.StartOf(materials, floor.Level);
            var entry = start is { } from ? FurniturePlanner.EntryOf(materials, from, floor.Level) : null;
            foreach (var cell in floor.Cells.OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X))
                if (cell.Material == BuildingMaterial.Floor && entry != cell.Cell && !taken.Contains((floor.Level, cell.Cell)))
                    free.Add(new FurnitureSpot(floor.Level, cell.Cell, 0));
        }

        return free;
    }

    /// <summary>Onde dormir, do melhor ao pior: as camas do catálogo; senão piso livre (fora da entrada e da mobília); senão a entrada;
    /// senão piso com mobília; senão a escada; só por último a porta. Vazio só se a planta não tem célula andável (o chamador cai no sorteio de sempre).</summary>
    private static List<FurnitureSpot> RestingSpots(Building building, LivingWorld.Domain.Objects.ObjectCatalog? catalog)
    {
        // #242: com a viagem em metros todo dorminhoco e todo trabalhador entra no prédio na hora em que chega, e a lista dos lugares
        // (puro do prédio, do catálogo e da planta) era refeita a cada entrada: fica guardada enquanto a planta e o catálogo forem os mesmos
        var cached = BuildingLayoutCache.Of(building);
        if (RestCache.TryGetValue(building, out var known) && ReferenceEquals(known.Layout, cached) && ReferenceEquals(known.Catalog, catalog))
            return known.Spots;
        var spots = ComputeRestingSpots(building, catalog);
        RestCache.AddOrUpdate(building, new RestPlan(cached, catalog, spots));
        return spots;
    }

    private sealed record RestPlan(object Layout, LivingWorld.Domain.Objects.ObjectCatalog? Catalog, List<FurnitureSpot> Spots);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Building, RestPlan> RestCache = new();

    private static List<FurnitureSpot> ComputeRestingSpots(Building building, LivingWorld.Domain.Objects.ObjectCatalog? catalog)
    {
        var layout = BuildingLayoutCache.Of(building).Layout;
        var furniture = catalog is null ? [] : HomeFurniture.Of(building, catalog);
        if (catalog is not null && HomeFurniture.BedSpots(building, catalog) is { Count: > 0 } beds) return [.. beds];

        var taken = furniture.Where(p => p.Spot is not null).Select(p => p.Spot!.Value).Select(s => (s.Floor, s.Cell)).ToHashSet();
        var free = new List<FurnitureSpot>();
        var entries = new List<FurnitureSpot>();
        var furnished = new List<FurnitureSpot>();
        var stairs = new List<FurnitureSpot>();
        var doors = new List<FurnitureSpot>();
        foreach (var floor in layout.FloorLayouts)
        {
            var materials = floor.Cells.ToDictionary(c => c.Cell, c => c.Material);
            var start = FurniturePlanner.StartOf(materials, floor.Level);
            var entry = start is { } from ? FurniturePlanner.EntryOf(materials, from, floor.Level) : null;
            foreach (var cell in floor.Cells.OrderBy(c => c.Cell.Y).ThenBy(c => c.Cell.X))
            {
                var spot = new FurnitureSpot(floor.Level, cell.Cell, 0);
                switch (cell.Material)
                {
                    case BuildingMaterial.Floor when taken.Contains((floor.Level, cell.Cell)): furnished.Add(spot); break;
                    case BuildingMaterial.Floor when entry == cell.Cell: entries.Add(spot); break;
                    case BuildingMaterial.Floor: free.Add(spot); break;
                    case BuildingMaterial.Stair: stairs.Add(spot); break;
                    case BuildingMaterial.Door: doors.Add(spot); break;
                }
            }
        }

        foreach (var tier in new[] { free, entries, furnished, stairs, doors })
            if (tier.Count > 0) return tier;
        return [];
    }
}
