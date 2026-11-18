using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Venture;
using LivingWorld.Simulation.Population.Naming;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population.Naming;

/// <summary>AD-186: outros feitos viram boa fama (mestria, filho criado, prosperidade, coragem), cada um com a sua chave; quem
/// abre negócio chama antes o sócio de nome melhor.</summary>
public class HonorDeedsTests
{
    private static readonly Personality Kind = Personality.Create(50, 50, 50, 50, 100, 50, 50, 50, 50, 50).Value!;

    private static WorldState Empty(NameBondRules? names = null) => ScenarioRunner.Create(1, initialPopulation: 0,
        familyRules: names is null ? null : ScenarioRunner.DefaultFamilyRules with { Names = names }).World;

    private static Npc Person(WorldState world, long id, string name, int age = 30, NpcId? mother = null, NpcId? father = null)
    {
        var city = world.ActiveCities().First();
        var npc = new Npc(new NpcId(id), name, Sex.Male, world.CurrentDate.AddYears(-age), ScenarioRunner.DefaultCulture, city.Location,
            motherId: mother, fatherId: father, household: null, health: 100, personality: Kind, profession: ProfessionType.None,
            currentLocation: city.Location, city: city.Id);
        world.AddNpc(npc);
        return npc;
    }

    private static long Now(WorldState world) => world.CurrentDate.TotalHours;

    [Fact]
    public void Mastery_is_honored_once_and_only_at_the_mastery_line()
    {
        var world = Empty();
        var smith = Person(world, 1, "Tor Galmor");

        Honor.OnMastery(world, smith, skill: 70, cap: 100, Now(world));
        Assert.Null(world.Honor);

        Honor.OnMastery(world, smith, skill: 85, cap: 100, Now(world));
        Honor.OnMastery(world, smith, skill: 90, cap: 100, Now(world));
        Assert.Equal(10.0, Honor.OwnOf(world, smith.Id.Value, Now(world)), 3);

        var off = Empty(NameBondRules.Default with { HonorPerMastery = 0 });
        var other = Person(off, 1, "Tor Galmor");
        Honor.OnMastery(off, other, skill: 90, cap: 100, Now(off));
        Assert.Null(off.Honor);
    }

    [Fact]
    public void A_child_grown_to_adulthood_honors_both_living_parents()
    {
        var world = Empty();
        var mother = Person(world, 1, "Ana Galmor");
        var father = Person(world, 2, "Tor Galmor");
        var son = Person(world, 3, "Ren Galmor", age: 15, mother: mother.Id, father: father.Id);
        father.Die(world.CurrentDate);

        Honor.OnChildRaised(world, son, Now(world));

        Assert.Equal((3.0, 0.0), (Honor.OwnOf(world, mother.Id.Value, Now(world)), Honor.OwnOf(world, father.Id.Value, Now(world))));
    }

    [Fact]
    public void Visible_profit_and_surviving_an_attack_are_honor_small_profit_and_city_draws_are_not()
    {
        var world = Empty();
        var owner = Person(world, 1, "Tor Galmor");
        var defender = Person(world, 2, "Ren Dunbar");
        var attacker = Person(world, 3, "Vil Dunbar");
        long minWage = world.EconomyRules.WageByProfession.Values.Min();

        Honor.OnEvent(world, WorldEventKind.ProfitDrawn, $"9|{owner.Id.Value}|{minWage}", Now(world));
        Honor.OnEvent(world, WorldEventKind.ProfitDrawn, $"9|city-x|{100 * minWage}", Now(world));
        Assert.Null(world.Honor);
        Honor.OnEvent(world, WorldEventKind.ProfitDrawn, $"9|{owner.Id.Value}|{4 * minWage}", Now(world));
        Assert.Equal(2.0, Honor.OwnOf(world, owner.Id.Value, Now(world)), 3);

        Honor.OnEvent(world, WorldEventKind.CombatResolved, $"{attacker.Id.Value}|{defender.Id.Value}|Resolved|3", Now(world));
        Assert.Equal(0.0, Honor.OwnOf(world, defender.Id.Value, Now(world)), 3); // o agressor ainda vive: ninguém venceu
        attacker.Die(world.CurrentDate);
        Honor.OnEvent(world, WorldEventKind.CombatResolved, $"{attacker.Id.Value}|{defender.Id.Value}|Resolved|3", Now(world));
        Assert.Equal(10.0, Honor.OwnOf(world, defender.Id.Value, Now(world)), 3);
    }

    [Fact]
    public void The_founder_calls_first_the_partner_with_the_better_name()
    {
        var world = Empty();
        var plain = Person(world, 1, "Tor Galmor");
        var honored = Person(world, 2, "Ren Dunbar");
        Honor.Credit(world, honored.Id.Value, 40, Now(world));

        Assert.Equal([2L, 1L], VentureCapital.PartnerOrder(world, [plain, honored]).Select(n => n.Id.Value));

        var off = Empty(NameBondRules.Default with { ReputationPartnerOrder = false });
        var a = Person(off, 1, "Tor Galmor");
        var b = Person(off, 2, "Ren Dunbar");
        Honor.Credit(off, b.Id.Value, 40, Now(off));
        Assert.Equal([1L, 2L], VentureCapital.PartnerOrder(off, [a, b]).Select(n => n.Id.Value));
    }
}
