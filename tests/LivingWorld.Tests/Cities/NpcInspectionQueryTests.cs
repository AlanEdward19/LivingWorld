using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T14 (CITY-06): <see cref="NpcInspectionQuery"/> — única fonte de consulta de
/// inspeção compartilhada entre API e CLI. Falha para id morto/inexistente (AC #3 da story P1);
/// devolve identidade/família/profissão/atributos/rotina/memórias de um NPC vivo (AC #1).</summary>
public class NpcInspectionQueryTests
{
    private static WorldState MakeWorld(int population = 1) =>
        ScenarioRunner.Create(seed: 7, initialPopulation: population).World;

    [Fact]
    public void Inspect_succeeds_and_ensures_materialization_for_a_living_npc()
    {
        var world = MakeWorld();
        var npc = world.Npcs.First();

        var result = NpcInspectionQuery.Inspect(world, npc.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(world.FindNpc(npc.Id));
    }

    [Fact]
    public void Inspect_returns_dto_matching_identity_profession_attributes_and_routine_of_the_engine_state()
    {
        var world = MakeWorld();
        var npc = world.Npcs.First();

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(npc.Id, dto.Id);
        Assert.Equal(npc.Name, dto.Name);
        Assert.Equal(npc.Sex, dto.Sex);
        Assert.Equal(npc.AgeYears(world.CurrentDate), dto.AgeYears);
        Assert.Equal(npc.Culture, dto.Culture);
        Assert.Equal(npc.City, dto.City);
        Assert.Equal(npc.Household, dto.Household);
        Assert.Equal(npc.MotherId, dto.MotherId);
        Assert.Equal(npc.FatherId, dto.FatherId);
        Assert.Equal(npc.Spouse, dto.Spouse);
        Assert.Equal(npc.Profession, dto.Profession);
        Assert.Equal(npc.Employer, dto.Employer);
        Assert.Equal(npc.Health, dto.Health);
        Assert.Equal(npc.HungerAt(world.CurrentDate.TotalHours), dto.Hunger);
        Assert.Equal(npc.ThirstAt(world.CurrentDate.TotalHours), dto.Thirst);
        Assert.Equal(npc.SleepAt(world.CurrentDate.TotalHours), dto.Sleep);
        Assert.Equal(npc.SocialAt(world.CurrentDate.TotalHours), dto.Social);
        Assert.Equal(npc.Personality, dto.Personality);
        Assert.Equal(npc.Skills, dto.Skills);
        Assert.Equal(npc.CurrentLocation, dto.CurrentLocation);
        Assert.Equal(npc.CurrentAction, dto.CurrentAction);
        Assert.Equal(npc.ActionStartedAtTick, dto.ActionStartedAtTick);
    }

    [Fact]
    public void Inspect_always_returns_an_empty_memories_list_in_this_phase()
    {
        var world = MakeWorld();
        var npc = world.Npcs.First();

        var dto = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Empty(dto.Memories);
    }

    [Fact]
    public void Inspect_materializes_a_never_touched_aggregate_pool_member_on_demand()
    {
        // Fase 8, fix round 1, gap 2 (CITY-05 AC2 — Independent Test da spec): consultar um NPC
        // agregado nunca materializado via API/CLI (aqui, o mesmo caminho: NpcInspectionQuery)
        // faz o sistema materializá-lo sob demanda antes de responder.
        var world = MakeWorld();
        var city = new City(world.NextCityId(), world.Npcs.First().CurrentLocation, 0, null,
            new AggregatePopulationPool(3, 300, 250));
        world.AddCity(city);
        var neverTouchedId = new NpcId(world.NextNpcId);
        Assert.Null(world.FindNpc(neverTouchedId)); // pré-condição: nunca materializado

        var result = NpcInspectionQuery.Inspect(world, neverTouchedId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(world.FindNpc(neverTouchedId));
        Assert.Equal(neverTouchedId, result.Value!.Id);
    }

    [Fact]
    public void Inspect_fails_for_an_npc_id_that_does_not_exist()
    {
        var world = MakeWorld();

        var result = NpcInspectionQuery.Inspect(world, new NpcId(999_999));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Inspect_fails_for_a_dead_npc()
    {
        var world = MakeWorld();
        var npc = world.Npcs.First();
        npc.Die(world.CurrentDate);

        var result = NpcInspectionQuery.Inspect(world, npc.Id);

        Assert.False(result.IsSuccess);
    }
}
