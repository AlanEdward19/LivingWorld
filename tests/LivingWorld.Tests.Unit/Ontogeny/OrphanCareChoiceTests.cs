using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Ontogeny;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Ontogeny;

/// <summary>AD-186: a casa que fica só com crianças é vista no dia, os irmãos vão juntos e quem acolhe decide pelo laço e pelos
/// meios (antes: no fim do mês, o primeiro parente achado, um irmão por vez).</summary>
public class OrphanCareChoiceTests
{
    private const long Day = 24;
    private static readonly CellCoord Home = default;

    private static Npc Person(WorldState world, string name, double ageYears, long wallet = 0, NpcId? mother = null, NpcId? father = null)
    {
        var personality = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), name, Sex.Female, world.CurrentDate.AddHours(-(long)(ageYears * world.CurrentDate.Calendar.HoursPerYear)),
            new CultureId(1), Home, mother, father, null, 100, personality, new ProfessionType(1), Home,
            wallet: new Money(wallet), city: default);
        world.AddNpc(npc);
        return npc;
    }

    private static Household House(WorldState world, params Npc[] members)
    {
        var household = new Household(world.NextHouseholdIdAndAdvance(), Home, members[0].Id, members.Select(m => m.Id).ToList());
        world.AddHousehold(household);
        foreach (var m in members) m.JoinHousehold(household.Id);
        return household;
    }

    private static WorldState World(bool legacy = false) =>
        ScenarioRunner.Create(1, initialPopulation: 0, ontogenyRules: OntogenyDefaults.Human with { OrphanCareLegacy = legacy }).World;

    [Fact]
    public void A_house_left_with_only_children_is_taken_in_within_days_siblings_together()
    {
        foreach (bool legacy in new[] { false, true })
        {
            var world = World(legacy);
            var clock = new WorldClock([new OrphanCareSystem()]);
            clock.Run(world, 3 * Day); // passa do 1º dia do mês: o modo de antes só olhava no mês seguinte
            var grandmother = Person(world, "avo", 68, wallet: 500);
            var mother = Person(world, "mae", 30, mother: grandmother.Id);
            var girl = Person(world, "menina", 2.3, mother: mother.Id);
            var brother = Person(world, "irmao", 9, mother: mother.Id);
            mother.Die(world.CurrentDate);
            House(world, girl, brother);
            var grandmotherHome = House(world, grandmother);

            clock.Run(world, 2 * Day);

            if (legacy) Assert.NotEqual(grandmotherHome.Id, girl.Household);
            else Assert.Equal((grandmotherHome.Id, grandmotherHome.Id), (girl.Household!.Value, brother.Household!.Value));
        }
    }

    [Fact]
    public void Between_two_aunts_the_one_with_means_takes_the_children_in()
    {
        foreach (bool legacy in new[] { false, true })
        {
            var world = World(legacy);
            var clock = new WorldClock([new OrphanCareSystem()]);
            var grandmother = Person(world, "avo", 70);
            var poorAunt = Person(world, "tia-pobre", 35, mother: grandmother.Id); // menor id: o de antes levava a ela
            var mother = Person(world, "mae", 30, mother: grandmother.Id);
            var richAunt = Person(world, "tia-com-meios", 38, wallet: 50_000, mother: grandmother.Id);
            var child = Person(world, "orfa", 5, mother: mother.Id);
            grandmother.Die(world.CurrentDate);
            mother.Die(world.CurrentDate);
            House(world, child);
            var poorHome = House(world, poorAunt, Person(world, "primo1", 3), Person(world, "primo2", 6));
            var richHome = House(world, richAunt);

            clock.Run(world, 32 * Day); // o modo de antes só olha uma vez por mês

            Assert.Equal(legacy ? poorHome.Id : richHome.Id, child.Household);
        }
    }
}
