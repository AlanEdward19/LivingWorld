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
    public void Inspect_succeeds_for_a_living_npc()
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
    public void MaterializeAndInspect_materializes_a_never_touched_aggregate_pool_member_on_demand()
    {
        // Fase 8, fix round 1, gap 2 (CITY-05 AC2 — Independent Test da spec): consultar um NPC
        // agregado nunca materializado via o comando explícito faz o sistema materializá-lo sob
        // demanda antes de responder — mesma invariante de sempre, agora fora do GET (T49).
        var world = MakeWorld();
        var poolNpcIds = world.ReserveNpcIdBlock(3);
        var city = new City(world.NextCityId(), world.Npcs.First().CurrentLocation, 0, null,
            new AggregatePopulationPool(3, 300, 250), poolNpcIds: poolNpcIds);
        world.AddCity(city);
        var neverTouchedId = poolNpcIds[0];
        Assert.Null(world.FindNpc(neverTouchedId)); // pré-condição: nunca materializado

        var result = NpcInspectionQuery.MaterializeAndInspect(world, neverTouchedId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(world.FindNpc(neverTouchedId));
        Assert.Equal(neverTouchedId, result.Value!.Id);
    }

    // --- Fase 15.1, T49 (backend-gaps.md G9): Inspect é leitura pura ---

    [Fact]
    public void Inspect_returns_pooled_lod_without_materializing_a_never_touched_aggregate_pool_member()
    {
        // T50: antes, todo id de pool caía no Fail genérico (a UI mostrava sempre "não
        // materializado" sem opção nenhuma). Agora um id reservado de verdade (City.PoolNpcIds)
        // devolve um DTO mínimo com Lod.Pooled — Inspect continua puro (nunca materializa).
        var world = MakeWorld();
        var poolNpcIds = world.ReserveNpcIdBlock(3);
        var city = new City(world.NextCityId(), world.Npcs.First().CurrentLocation, 0, null,
            new AggregatePopulationPool(3, 300, 250), poolNpcIds: poolNpcIds);
        world.AddCity(city);
        var neverTouchedId = poolNpcIds[0];
        var canonicalHashBefore = WorldSnapshot.CanonicalHash(world);
        var pendingEventCountBefore = world.PendingEvents.Count;

        var result = NpcInspectionQuery.Inspect(world, neverTouchedId);

        Assert.True(result.IsSuccess);
        Assert.Equal(NpcInspectionLod.Pooled, result.Value!.Lod);
        Assert.Equal(neverTouchedId, result.Value!.Id);
        Assert.Null(world.FindNpc(neverTouchedId)); // nunca materializou
        Assert.Equal(canonicalHashBefore, WorldSnapshot.CanonicalHash(world)); // hash intocado
        Assert.Equal(pendingEventCountBefore, world.PendingEvents.Count); // nenhum evento novo agendado
    }

    [Fact]
    public void Inspect_fails_for_an_id_reserved_by_no_city_and_never_materialized()
    {
        var world = MakeWorld();
        var neverReservedId = new NpcId(world.NextNpcId);

        var result = NpcInspectionQuery.Inspect(world, neverReservedId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Repeated_Inspect_calls_on_the_same_living_npc_are_idempotent()
    {
        var world = MakeWorld();
        var npc = world.Npcs.First();

        var first = NpcInspectionQuery.Inspect(world, npc.Id).Value!;
        var second = NpcInspectionQuery.Inspect(world, npc.Id).Value!;
        var third = NpcInspectionQuery.Inspect(world, npc.Id).Value!;

        Assert.Equal(first, second);
        Assert.Equal(second, third);
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
