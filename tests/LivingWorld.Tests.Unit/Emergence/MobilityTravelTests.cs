using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Emergence;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Emergence;

/// <summary>#61 fatia A: a tecnologia de mobilidade adotada encurta todo deslocamento do NPC, não só a despensa.</summary>
public class MobilityTravelTests
{
    private static readonly EmergenceRules Rules = EmergenceRules.Mobility;

    private static void ForceAdoption(WorldState world)
    {
        var cities = world.ActiveCities().Select(c => c.Id).ToList();
        world.AddTechnology(new Technology(
            world.Npcs.First().Culture, EmergenceRules.MobilityCategory, new TechnologyVector([7, 0, 0]), 0, cities[0], null, cities));
    }

    [Fact]
    public void Travel_ticks_shrink_with_the_adopted_pace_never_below_one_and_zero_when_already_there()
    {
        var (world, _) = ScenarioRunner.Create(3, emergenceRules: Rules);
        var npc = world.Npcs.First();
        var far = new CellCoord(npc.CurrentLocation.X + 12, npc.CurrentLocation.Y + 12);
        long baseTicks = Mobility.TravelTicks(world, npc, npc.CurrentLocation, far, 1.0);
        long adjacent = Mobility.TravelTicks(world, npc, npc.CurrentLocation, new CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y), 1.0);

        ForceAdoption(world);

        Assert.True(baseTicks > 1);
        Assert.True(Mobility.TravelTicks(world, npc, npc.CurrentLocation, far, 1.0) < baseTicks);
        Assert.Equal(Math.Max(1, adjacent), Math.Max(1, Mobility.TravelTicks(world, npc, npc.CurrentLocation, new CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y), 1.0)));
        Assert.True(Mobility.TravelTicks(world, npc, npc.CurrentLocation, new CellCoord(npc.CurrentLocation.X + 1, npc.CurrentLocation.Y), 1.0) >= 1);
        Assert.Equal(0, Mobility.TravelTicks(world, npc, npc.CurrentLocation, npc.CurrentLocation, 1.0));
    }

    private static double TravelShare(ulong seed, bool adopted)
    {
        var (world, clock) = ScenarioRunner.Create(seed, emergenceRules: Rules);
        if (adopted) ForceAdoption(world);
        long travel = 0, total = 0;
        for (int i = 0; i < world.Calendar.HoursPerDay * 30; i++)
        {
            clock.Run(world, 1);
            foreach (var npc in world.Npcs)
            {
                if (!npc.IsAlive) continue;
                total++;
                if (npc.CurrentAction == ActionType.Travel) travel++;
            }
        }
        return (double)travel / Math.Max(1, total);
    }

    [Fact]
    public void Adopted_mobility_lowers_the_share_of_npc_hours_spent_travelling_in_most_seeds()
    {
        int lower = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (TravelShare(seed, true) < TravelShare(seed, false)) lower++;

        Assert.True(lower >= 18, $"só {lower}/20 seeds reduziram a fração em Travel");
    }

    [Fact]
    public void Turning_mobility_off_changes_the_canonical_hash()
    {
        var (on, clockOn) = ScenarioRunner.Create(5, emergenceRules: Rules);
        var (off, clockOff) = ScenarioRunner.Create(5, emergenceRules: Rules);
        ForceAdoption(on);
        clockOn.Run(on, on.Calendar.HoursPerYear * 10);
        clockOff.Run(off, off.Calendar.HoursPerYear * 10);

        Assert.NotEqual(WorldSnapshot.CanonicalHash(on), WorldSnapshot.CanonicalHash(off));
    }
}
