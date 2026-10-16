using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Cities;

/// <summary>npc-building-occupancy C7–C11: o NPC aparece dentro do prédio quando ação e lugar pedem.</summary>
public class BuildingOccupancySystemTests
{
    private static (WorldState World, Npc Npc) Setup()
    {
        var world = ScenarioRunner.Create(seed: 41, initialPopulation: 40).World;
        var household = world.Households.First(h => h.Dwelling is not null);
        var npc = world.Npcs.First(n => n.Household == household.Id);
        npc.MoveTo(household.Location, 0);
        return (world, npc);
    }

    private static void Tick(WorldState world) =>
        new BuildingOccupancySystem().Tick(world, new TickContext(world, world.Rng, world.Scheduler));

    [Fact]
    public void Sleep_enters_the_dwelling_when_at_the_household_cell()
    {
        var (world, npc) = Setup();
        npc.SetCurrentAction(ActionType.Sleep, 0);

        Tick(world);

        var dwelling = world.FindHousehold(npc.Household!.Value)!.Dwelling;
        Assert.Equal(dwelling, npc.Interior!.Building);
        var floors = BuildingLayoutGenerator.FloorsOf(dwelling!.Value, world.FindBuilding(dwelling.Value)!.BuildingTypeId);
        Assert.InRange(npc.Interior.Floor.Value, 0, floors - 1);
    }

    [Fact]
    public void Sleep_away_from_the_household_cell_stays_outside()
    {
        var (world, npc) = Setup();
        npc.MoveTo(new LivingWorld.Domain.Geography.CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y), 0);
        npc.SetCurrentAction(ActionType.Sleep, 0);

        Tick(world);

        Assert.Null(npc.Interior);
    }

    [Fact]
    public void Work_enters_the_workplace_building_on_the_ground_floor()
    {
        var (world, npc) = Setup();
        var workplace = world.Workplaces.First(w => w.Building is not null);
        npc.Hire(workplace.Id);
        npc.MoveTo(workplace.Location, 0);
        npc.SetCurrentAction(ActionType.Work, 0);

        Tick(world);

        Assert.Equal(workplace.Building, npc.Interior!.Building);
        Assert.Equal(0, npc.Interior.Floor.Value);
    }

    [Fact]
    public void Leaving_the_action_exits_the_building()
    {
        var (world, npc) = Setup();
        npc.SetCurrentAction(ActionType.Sleep, 0);
        Tick(world);
        Assert.NotNull(npc.Interior);

        npc.SetCurrentAction(ActionType.Eat, 1);
        Tick(world);

        Assert.Null(npc.Interior);
    }

    [Fact]
    public void Placement_is_pure_in_range_and_walkable()
    {
        foreach (var type in new[] { -1, 0, 1, 2, 7 })
            for (long b = 1; b <= 20; b++)
            {
                var building = new Building(new BuildingId(b), default, type, 0);
                var layout = BuildingLayoutGenerator.Generate(building);
                for (long n = 1; n <= 30; n++)
                    foreach (var ground in new[] { false, true })
                    {
                        var first = BuildingOccupancySystem.Place(new NpcId(n), building, ground);
                        Assert.Equal(first, BuildingOccupancySystem.Place(new NpcId(n), building, ground));
                        Assert.InRange(first.Floor.Value, 0, layout.Floors - 1);
                        if (ground) Assert.Equal(0, first.Floor.Value);
                        var cell = layout.FloorLayouts[first.Floor.Value].Cells.Single(c => c.Cell == first.Cell);
                        Assert.True(InteriorWalkability.IsWalkable(cell.Material));
                    }
            }
    }

    [Fact]
    public void Same_seed_same_interior_assignments()
    {
        static Dictionary<long, (long, int, int, int)> Run()
        {
            var (world, clock) = ScenarioRunner.Create(seed: 77, initialPopulation: 60);
            clock.Run(world, 24 * 14);
            return world.Npcs.Where(n => n.Interior is not null)
                .ToDictionary(n => n.Id.Value, n => (n.Interior!.Building.Value, n.Interior.Floor.Value, n.Interior.LocalCell.X, n.Interior.LocalCell.Y));
        }

        var first = Run();

        Assert.NotEmpty(first);
        Assert.Equal(first, Run());
    }

    [Fact]
    public void Dead_npc_or_missing_building_stays_outside()
    {
        var (world, npc) = Setup();
        npc.SetCurrentAction(ActionType.Sleep, 0);
        world.FindHousehold(npc.Household!.Value)!.LiveIn(new BuildingId(9_999_999));

        Tick(world);

        Assert.Null(npc.Interior);
    }

    [Fact]
    public void Dead_npc_is_taken_out_of_the_building()
    {
        var (world, npc) = Setup();
        npc.SetCurrentAction(ActionType.Sleep, 0);
        Tick(world);
        Assert.NotNull(npc.Interior);

        npc.Die(world.CurrentDate);
        Tick(world);

        Assert.Null(npc.Interior);
    }

    [Fact]
    public void Sleepers_spread_over_the_floors_of_a_multi_floor_house()
    {
        var world = ScenarioRunner.Create(seed: 41, initialPopulation: 40).World;
        var floors = new HashSet<int>();
        foreach (var household in world.Households.Where(h => h.Dwelling is not null))
            foreach (var npc in world.Npcs.Where(n => n.Household == household.Id))
            {
                npc.MoveTo(household.Location, 0);
                npc.SetCurrentAction(ActionType.Sleep, 0);
            }

        Tick(world);

        foreach (var npc in world.Npcs.Where(n => n.Interior is not null)) floors.Add(npc.Interior!.Floor.Value);
        Assert.Contains(0, floors);
        Assert.Contains(1, floors);
    }
}
