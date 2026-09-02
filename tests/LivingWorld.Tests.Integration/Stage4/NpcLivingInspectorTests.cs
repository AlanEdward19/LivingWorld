using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Stage4;

public class NpcLivingInspectorTests
{
    private static (WorldState World, Npc Npc) Subject()
    {
        var world = ScenarioRunner.Create(seed: 151, initialPopulation: 2).World;
        return (world, world.Npcs.OrderBy(npc => npc.Id.Value).First());
    }

    [Fact]
    public void Inspection_exposes_identity_family_needs_health_job_and_skills_from_canonical_state()
    {
        var (world, npc) = Subject();
        npc.JoinHousehold(new HouseholdId(41));
        npc.Hire(new WorkplaceId(52));
        npc.GainSkill(new SkillType(7), 12.5, 100);
        npc.SetHunger(63, world.CurrentDate.TotalHours);
        npc.SetThirst(72, world.CurrentDate.TotalHours);
        npc.SetSleep(81, world.CurrentDate.TotalHours);
        npc.SetSocial(54, world.CurrentDate.TotalHours);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(npc.Name, dto.Name);
        Assert.Equal(new HouseholdId(41), dto.Household);
        Assert.Equal((63, 72, 81, 54), (dto.Hunger, dto.Thirst, dto.Sleep, dto.Social));
        Assert.Equal(npc.Health, dto.Health);
        Assert.Equal(new WorkplaceId(52), dto.Employer);
        Assert.Equal(12.5, dto.Skills.Get(new SkillType(7)));
    }

    [Fact]
    public void Work_action_targets_the_canonical_employer()
    {
        var (world, npc) = Subject();
        npc.Hire(new WorkplaceId(52));
        npc.SetCurrentAction(ActionType.Work, 9);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(new NpcActionTargetDto("workplace", "52"), dto.ActionTarget);
    }

    [Fact]
    public void Sleep_action_targets_the_canonical_household()
    {
        var (world, npc) = Subject();
        npc.JoinHousehold(new HouseholdId(41));
        npc.SetCurrentAction(ActionType.Sleep, 10);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(new NpcActionTargetDto("household", "41"), dto.ActionTarget);
    }

    [Fact]
    public void Socialize_action_targets_the_canonical_spouse()
    {
        var (world, npc) = Subject();
        npc.Marry(new NpcId(88));
        npc.SetCurrentAction(ActionType.Socialize, 11);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(new NpcActionTargetDto("npc", "88"), dto.ActionTarget);
    }

    [Fact]
    public void Action_without_a_persisted_target_does_not_invent_one()
    {
        var (world, npc) = Subject();
        npc.SetCurrentAction(ActionType.Idle, 12);

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Null(dto.ActionTarget);
    }

    [Fact]
    public void Live_identity_is_materialized_while_aggregate_population_stays_anonymous_count()
    {
        var (world, npc) = Subject();
        var city = new City(world.NextCityId(), npc.CurrentLocation, world.CurrentDate.TotalHours, null,
            new AggregatePopulationPool(4, 40, 320));
        world.AddCity(city);
        var poolBefore = city.AggregatePool;

        var live = NpcInspectionQuery.Inspect(world, npc.Id).Value!;
        var anonymous = NpcInspectionQuery.Inspect(world, new NpcId(world.NextNpcId));

        Assert.Equal(NpcInspectionLod.Materialized, live.Lod);
        Assert.False(anonymous.IsSuccess);
        Assert.Equal(poolBefore, city.AggregatePool);
    }
}
