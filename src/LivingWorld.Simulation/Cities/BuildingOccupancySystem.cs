using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Core;
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

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        foreach (var npc in world.Npcs)
        {
            var target = TargetBuilding(world, npc);
            if (target is null)
            {
                if (npc.Interior is not null) npc.ExitBuilding();
                continue;
            }

            // Andar e célula são puros em (NpcId, BuildingId): quem já está no prédio certo não muda de lugar.
            if (npc.Interior?.Building == target.Value.Building.Id) continue;
            var (building, floor, cell) = Place(npc.Id, target.Value.Building, target.Value.GroundOnly);
            InteriorEntry.TryEnter(world, npc, building, floor, cell);
        }
    }

    private static (Building Building, bool GroundOnly)? TargetBuilding(WorldState world, Npc npc)
    {
        if (!npc.IsAlive) return null;

        BuildingId? target = null;
        bool ground = false;
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
                    target = workplaceBuilding;
                break;
        }

        if (target is not { } buildingId || world.FindBuilding(buildingId) is not { } building) return null;
        return (building, ground);
    }

    /// <summary>Andar (térreo quando <paramref name="groundOnly"/>) e célula caminhável, escolhidos só por hash
    /// estável de <c>(NpcId, BuildingId)</c>.</summary>
    internal static (BuildingId Building, FloorLevel Floor, CellCoord Cell) Place(NpcId npc, Building building, bool groundOnly)
    {
        var layout = BuildingLayoutGenerator.Generate(building);
        ulong hash = StableHash.Mix(npc.Value ^ (building.Id.Value << 20));
        int floorIndex = groundOnly ? 0 : (int)(hash % (ulong)layout.Floors);
        var walkable = layout.FloorLayouts[floorIndex].Cells
            .Where(c => InteriorWalkability.IsWalkable(c.Material))
            .ToArray();
        var chosen = walkable[(int)(StableHash.Mix((long)hash) % (ulong)walkable.Length)];
        return (building.Id, new FloorLevel(floorIndex), chosen.Cell);
    }
}
