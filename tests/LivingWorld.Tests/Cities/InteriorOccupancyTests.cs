using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 15.1, T47 (backend-gaps.md G7): escopo de interior do NPC (prédio/andar/célula
/// local) sem perder localização global, projeção real de ocupantes e deltas de transição.</summary>
public class InteriorOccupancyTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality SomePersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(WorldState world, CellCoord globalLocation, CityId city)
    {
        var npcId = world.NextNpcIdAndAdvance();
        var npc = new Npc(
            npcId, $"npc-{npcId.Value}", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), globalLocation,
            motherId: null, fatherId: null, household: null, health: 100, personality: SomePersonality,
            profession: new ProfessionType(1), currentLocation: globalLocation, city: city);
        world.AddNpc(npc);
        return npc;
    }

    // --- Npc: entrar / mover / trocar andar / sair ---

    [Fact]
    public void Entering_a_building_sets_Interior_without_touching_global_location_or_city()
    {
        var world = ScenarioRunner.Create(seed: 1, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(5, 5), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var npc = MakeNpc(world, globalLocation: new CellCoord(5, 5), city.Id);
        var buildingId = new BuildingId(1);

        npc.EnterBuilding(buildingId, FloorLevel.Ground, new CellCoord(2, 2));

        Assert.NotNull(npc.Interior);
        Assert.Equal(buildingId, npc.Interior!.Building);
        Assert.Equal(FloorLevel.Ground, npc.Interior.Floor);
        Assert.Equal(new CellCoord(2, 2), npc.Interior.LocalCell);
        Assert.Equal(new CellCoord(5, 5), npc.CurrentLocation); // localização global intocada
        Assert.Equal(city.Id, npc.City);
    }

    [Fact]
    public void Moving_within_a_building_keeps_the_same_building_and_floor()
    {
        var world = ScenarioRunner.Create(seed: 2, initialPopulation: 0).World;
        var npc = MakeNpc(world, new CellCoord(0, 0), default);
        npc.EnterBuilding(new BuildingId(1), FloorLevel.Ground, new CellCoord(1, 1));

        npc.MoveWithinBuilding(new CellCoord(3, 3));

        Assert.Equal(new BuildingId(1), npc.Interior!.Building);
        Assert.Equal(FloorLevel.Ground, npc.Interior.Floor);
        Assert.Equal(new CellCoord(3, 3), npc.Interior.LocalCell);
    }

    [Fact]
    public void Changing_floor_keeps_the_same_building_and_updates_floor_and_cell()
    {
        var world = ScenarioRunner.Create(seed: 3, initialPopulation: 0).World;
        var npc = MakeNpc(world, new CellCoord(0, 0), default);
        npc.EnterBuilding(new BuildingId(1), FloorLevel.Ground, new CellCoord(1, 1));

        npc.ChangeFloor(FloorNavigator.Up(FloorLevel.Ground), new CellCoord(0, 0));

        Assert.Equal(new BuildingId(1), npc.Interior!.Building);
        Assert.Equal(new FloorLevel(1), npc.Interior.Floor);
        Assert.Equal(new CellCoord(0, 0), npc.Interior.LocalCell);
    }

    [Fact]
    public void Exiting_clears_Interior_and_leaves_global_location_untouched()
    {
        var world = ScenarioRunner.Create(seed: 4, initialPopulation: 0).World;
        var npc = MakeNpc(world, new CellCoord(7, 8), default);
        npc.EnterBuilding(new BuildingId(1), FloorLevel.Ground, new CellCoord(1, 1));

        npc.ExitBuilding();

        Assert.Null(npc.Interior);
        Assert.Equal(new CellCoord(7, 8), npc.CurrentLocation);
    }

    [Fact]
    public void Moving_or_changing_floor_without_first_entering_a_building_throws_scope_exclusivity()
    {
        var world = ScenarioRunner.Create(seed: 5, initialPopulation: 0).World;
        var npc = MakeNpc(world, new CellCoord(0, 0), default);

        Assert.Throws<InvalidOperationException>(() => npc.MoveWithinBuilding(new CellCoord(1, 1)));
        Assert.Throws<InvalidOperationException>(() => npc.ChangeFloor(FloorLevel.Ground, new CellCoord(1, 1)));
    }

    [Fact]
    public void Interior_occupancy_survives_a_snapshot_round_trip()
    {
        var (world, _) = ScenarioRunner.Create(seed: 6);
        var npc = world.Npcs[0];
        npc.EnterBuilding(new BuildingId(1), new FloorLevel(2), new CellCoord(3, 4));

        var rehydrated = WorldSnapshot.Deserialize(WorldSnapshot.Serialize(world));

        var rehydratedNpc = rehydrated.Npcs.Single(n => n.Id == npc.Id);
        Assert.Equal(new BuildingId(1), rehydratedNpc.Interior!.Building);
        Assert.Equal(new FloorLevel(2), rehydratedNpc.Interior.Floor);
        Assert.Equal(new CellCoord(3, 4), rehydratedNpc.Interior.LocalCell);
    }

    [Fact]
    public void Entering_the_same_building_floor_and_cell_twice_produces_the_same_result()
    {
        var world = ScenarioRunner.Create(seed: 7, initialPopulation: 0).World;
        var npc = MakeNpc(world, new CellCoord(0, 0), default);

        npc.EnterBuilding(new BuildingId(9), new FloorLevel(1), new CellCoord(2, 2));
        var first = npc.Interior;
        npc.EnterBuilding(new BuildingId(9), new FloorLevel(1), new CellCoord(2, 2));
        var second = npc.Interior;

        Assert.Equal(first, second);
    }

    // --- InteriorProjector: ocupantes reais ---

    [Fact]
    public void Build_lists_only_alive_npcs_currently_inside_the_requested_building()
    {
        var world = ScenarioRunner.Create(seed: 8, initialPopulation: 0).World;
        var city = new City(world.NextCityId(), new CellCoord(0, 0), 0, null, AggregatePopulationPool.Empty);
        world.AddCity(city);
        var buildingA = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
        var buildingB = new Building(world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId: 1, completedAtTick: 0);
        world.AddBuilding(buildingA);
        world.AddBuilding(buildingB);

        var inside = MakeNpc(world, new CellCoord(0, 0), city.Id);
        inside.EnterBuilding(buildingA.Id, FloorLevel.Ground, new CellCoord(1, 1));
        var elsewhere = MakeNpc(world, new CellCoord(0, 0), city.Id);
        elsewhere.EnterBuilding(buildingB.Id, FloorLevel.Ground, new CellCoord(1, 1));
        var outside = MakeNpc(world, new CellCoord(0, 0), city.Id);
        var dead = MakeNpc(world, new CellCoord(0, 0), city.Id);
        dead.EnterBuilding(buildingA.Id, FloorLevel.Ground, new CellCoord(2, 2));
        dead.Die(WorldDate.Epoch(Calendar).AddHours(1));

        var result = InteriorProjector.Build(world, buildingA.Id);

        Assert.True(result.IsSuccess);
        var occupant = Assert.Single(result.Value!.Occupants);
        Assert.Equal(inside.Id, occupant.Npc);
        Assert.Equal(new CellCoord(1, 1), occupant.LocalCell);
    }

    // --- InteriorOccupancyDiffer: entrar / mover / trocar andar / sair ---

    [Fact]
    public void Diff_classifies_entered_moved_changed_floor_and_exited_correctly()
    {
        var stayedSamePlace = new InteriorOccupant(new NpcId(1), FloorLevel.Ground, new CellCoord(0, 0));
        var movedBefore = new InteriorOccupant(new NpcId(2), FloorLevel.Ground, new CellCoord(0, 0));
        var movedAfter = movedBefore with { LocalCell = new CellCoord(5, 5) };
        var floorChangedBefore = new InteriorOccupant(new NpcId(3), FloorLevel.Ground, new CellCoord(1, 1));
        var floorChangedAfter = floorChangedBefore with { Floor = new FloorLevel(1) };
        var exited = new InteriorOccupant(new NpcId(4), FloorLevel.Ground, new CellCoord(0, 0));
        var entered = new InteriorOccupant(new NpcId(5), FloorLevel.Ground, new CellCoord(0, 0));

        var before = new[] { stayedSamePlace, movedBefore, floorChangedBefore, exited };
        var after = new[] { stayedSamePlace, movedAfter, floorChangedAfter, entered };

        var delta = InteriorOccupancyDiffer.Diff(before, after);

        Assert.Equal([entered], delta.Entered);
        Assert.Equal([movedAfter], delta.Moved);
        Assert.Equal([floorChangedAfter], delta.ChangedFloor);
        Assert.Equal([exited.Npc], delta.Exited);
    }
}
