using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-144: o dono das oficinas iniciais sai de uma regra do mundo. A oficina de ofício (ferraria) é do mestre, o adulto
/// do ofício com mais habilidade na cidade; a terra da fazenda é comum (sem dono, excedente da cidade).</summary>
public sealed class WorkplaceOwnershipTests
{
    [Fact]
    public void The_forge_belongs_to_the_most_skilled_grown_smith_of_its_city()
    {
        var (world, _) = ScenarioRunner.Create(seed: 11);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        var skill = ScenarioRunner.DefaultSkillsRules.SkillByProfession[2];
        var smiths = world.Npcs.Where(n => n.IsAlive && n.Profession.Id == 2 && n.City == forge.City
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) != LifeStage.Child).ToList();
        double best = smiths.Max(n => n.Skills.Get(skill));

        var owner = world.FindNpc(forge.Owner!.Value)!;

        Assert.Equal(best, owner.Skills.Get(skill));
    }

    [Fact]
    public void The_farm_is_common_land_without_an_owner()
    {
        var (world, _) = ScenarioRunner.Create(seed: 11);

        Assert.Null(world.Workplaces.First(w => w.LocationType.Id == 1).Owner);
    }

    [Fact]
    public void A_craft_without_anyone_of_the_trade_stays_without_an_owner()
    {
        var (world, _) = ScenarioRunner.Create(seed: 11);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        forge.AssignOwner(null);
        foreach (var npc in world.Npcs.Where(n => n.Profession.Id == 2)) npc.SwitchProfession(new ProfessionType(1));

        WorkplaceOwnership.AssignFoundingMasters(world, ScenarioRunner.DefaultCraftLocationTypeIds);

        Assert.Null(forge.Owner);
    }

    [Fact]
    public void Without_a_craft_rule_nobody_owns_anything()
    {
        var (world, _) = ScenarioRunner.Create(seed: 11);
        foreach (var w in world.Workplaces) w.AssignOwner(null);

        WorkplaceOwnership.AssignFoundingMasters(world, []);

        Assert.All(world.Workplaces, w => Assert.Null(w.Owner));
    }
}
