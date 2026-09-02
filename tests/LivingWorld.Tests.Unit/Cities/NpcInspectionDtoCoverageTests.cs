using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Behavior;
using LivingWorld.Simulation.History;

namespace LivingWorld.Tests.Cities;

/// <summary>Fase 8, T21 (CITY-06): inspeção exaustiva por reflexão — mundo de 100 NPCs, itera
/// TODOS os vivos (sem sorteio), compara <see cref="NpcInspectionDto"/> campo a campo com o
/// estado do motor (<see cref="Npc"/>), mesmo padrão de sweep por reflexão de <see
/// cref="ReferentialIntegritySweepTests"/>/<see cref="MonotonicFieldsTests"/>: cada propriedade
/// do DTO precisa de uma entrada em <see cref="FieldCheckers"/>, e um campo novo sem entrada
/// reprova a própria checagem de cobertura (R5) em vez de passar em silêncio.</summary>
public class NpcInspectionDtoCoverageTests
{
    private const int PopulationCount = 100;

    /// <summary>Deriva o valor esperado de cada campo do DTO diretamente do <see cref="Npc"/>/
    /// <see cref="WorldState"/> — nunca relendo o próprio DTO — para que um bug de mapeamento em
    /// <see cref="NpcInspectionQuery.Inspect"/> (campo trocado, esquecido) derrube a
    /// comparação.</summary>
    private static readonly Dictionary<string, Func<NpcInspectionDto, Npc, WorldState, bool>> FieldCheckers = new()
    {
        [nameof(NpcInspectionDto.Id)] = (dto, npc, _) => dto.Id == npc.Id,
        [nameof(NpcInspectionDto.Name)] = (dto, npc, _) => dto.Name == npc.Name,
        [nameof(NpcInspectionDto.Sex)] = (dto, npc, _) => dto.Sex == npc.Sex,
        [nameof(NpcInspectionDto.AgeYears)] = (dto, npc, world) => dto.AgeYears == npc.AgeYears(world.CurrentDate),
        [nameof(NpcInspectionDto.Culture)] = (dto, npc, _) => dto.Culture == npc.Culture,
        [nameof(NpcInspectionDto.City)] = (dto, npc, _) => dto.City == npc.City,
        [nameof(NpcInspectionDto.Household)] = (dto, npc, _) => dto.Household == npc.Household,
        [nameof(NpcInspectionDto.MotherId)] = (dto, npc, _) => dto.MotherId == npc.MotherId,
        [nameof(NpcInspectionDto.FatherId)] = (dto, npc, _) => dto.FatherId == npc.FatherId,
        [nameof(NpcInspectionDto.Spouse)] = (dto, npc, _) => dto.Spouse == npc.Spouse,
        [nameof(NpcInspectionDto.Profession)] = (dto, npc, _) => dto.Profession.Equals(npc.Profession),
        [nameof(NpcInspectionDto.Employer)] = (dto, npc, _) => dto.Employer == npc.Employer,
        [nameof(NpcInspectionDto.Health)] = (dto, npc, _) => dto.Health == npc.Health,
        [nameof(NpcInspectionDto.Hunger)] = (dto, npc, world) => dto.Hunger == npc.HungerAt(world.CurrentDate.TotalHours),
        [nameof(NpcInspectionDto.Thirst)] = (dto, npc, world) => dto.Thirst == npc.ThirstAt(world.CurrentDate.TotalHours),
        [nameof(NpcInspectionDto.Sleep)] = (dto, npc, world) => dto.Sleep == npc.SleepAt(world.CurrentDate.TotalHours),
        [nameof(NpcInspectionDto.Social)] = (dto, npc, world) => dto.Social == npc.SocialAt(world.CurrentDate.TotalHours),
        [nameof(NpcInspectionDto.Personality)] = (dto, npc, _) => dto.Personality.Equals(npc.Personality),
        [nameof(NpcInspectionDto.Skills)] = (dto, npc, _) => dto.Skills.Equals(npc.Skills),
        [nameof(NpcInspectionDto.CurrentLocation)] = (dto, npc, _) => dto.CurrentLocation.Equals(npc.CurrentLocation),
        [nameof(NpcInspectionDto.CurrentAction)] = (dto, npc, _) => dto.CurrentAction == npc.CurrentAction,
        [nameof(NpcInspectionDto.ActionStartedAtTick)] = (dto, npc, _) => dto.ActionStartedAtTick == npc.ActionStartedAtTick,
        [nameof(NpcInspectionDto.ActionTarget)] = (dto, npc, _) => dto.ActionTarget == ExpectedTarget(npc),
        [nameof(NpcInspectionDto.Lod)] = (dto, _, _) => dto.Lod == NpcInspectionLod.Materialized,
        [nameof(NpcInspectionDto.Beliefs)] = (dto, npc, world) => dto.Beliefs.SequenceEqual(NpcBeliefQuery.BeliefsOf(world, npc.Id)),
        // Memórias é sempre lista vazia nesta fase (SPEC_DEVIATION do próprio DTO, Fase 10/11) —
        // não há campo de motor pra comparar contra; a checagem é o próprio contrato ("vazio").
        [nameof(NpcInspectionDto.Memories)] = (dto, _, _) => dto.Memories.Count == 0,
        [nameof(NpcInspectionDto.PowerIds)] = (dto, npc, world) => dto.PowerIds.SequenceEqual(
            world.ExtraordinaryCarriers.FirstOrDefault(carrier => carrier.CarrierId == npc.Id)?.PowerIds ?? []),
        // T50: mesmo critério geométrico de NpcScopeResolver — deriva independente do DTO, igual
        // a todo campo acima.
        [nameof(NpcInspectionDto.CurrentScope)] = (dto, npc, world) => dto.CurrentScope.Equals(ExpectedScope(npc, world)),
        [nameof(NpcInspectionDto.Rest)] = (dto, npc, world) => Equals(dto.Rest, RestPresentation.Of(world, npc)),
        [nameof(NpcInspectionDto.Food)] = (dto, npc, world) => Equals(dto.Food, FoodPresentation.Of(world, npc)),
        [nameof(NpcInspectionDto.CognitionTrace)] = (dto, npc, world) =>
            dto.CognitionTrace.SequenceEqual(world.CognitionLog.RecentEntries(npc.Id, int.MaxValue)),
    };

    private static NpcScope ExpectedScope(Npc npc, WorldState world)
    {
        var city = world.FindCity(npc.City);
        if (city is null) return new NpcScope(NpcScopeKind.World, null);
        var bounds = CityOccupancy.ResolveGrownBounds(
            world, city, CityPopulationQuery.Population(world, npc.City)).Bounds;
        return NpcScopeResolver.Resolve(npc, bounds);
    }

    private static NpcActionTargetDto? ExpectedTarget(Npc npc) => npc.CurrentAction switch
    {
        ActionType.Work when npc.Employer is { } workplace => new("workplace", workplace.Value.ToString()),
        ActionType.Sleep when npc.Household is { } household => new("household", household.Value.ToString()),
        ActionType.Socialize when npc.Spouse is { } spouse => new("npc", spouse.Value.ToString()),
        _ => null,
    };

    private static WorldState BuildWorldWith100Npcs()
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed: 21, ScenarioRunner.InitialMap(21, PopulationCount),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        PopulationSeeder.SeedInitial(world, PopulationCount, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);
        return world;
    }

    [Fact]
    public void Every_dto_property_has_a_registered_comparison()
    {
        foreach (var property in typeof(NpcInspectionDto).GetProperties())
            Assert.True(FieldCheckers.ContainsKey(property.Name), $"{property.Name}: campo novo do DTO sem comparação registrada em {nameof(FieldCheckers)}");
    }

    [Fact]
    public void All_100_living_npcs_match_the_engine_state_field_by_field_with_no_sampling()
    {
        var world = BuildWorldWith100Npcs();
        var aliveNpcs = world.Npcs.Where(n => n.IsAlive).ToList();
        Assert.Equal(PopulationCount, aliveNpcs.Count); // ninguem morreu antes da 1a inspecao - todos os 100 entram, sem sorteio

        int compared = 0;
        foreach (var npc in aliveNpcs)
        {
            var result = NpcInspectionQuery.Inspect(world, npc.Id);
            Assert.True(result.IsSuccess);
            var dto = result.Value!;

            foreach (var (field, checker) in FieldCheckers)
                Assert.True(checker(dto, npc, world), $"npc {npc.Id.Value}: campo {field} diverge do estado do motor");

            compared++;
        }

        Assert.Equal(PopulationCount, compared); // os 100 foram de fato comparados, nao so contados
    }

    // Check B (não-raso): prova que os checkers discriminam de verdade — comparar o DTO de um
    // NPC contra o estado de OUTRO deve falhar em pelo menos um campo identity-like.
    [Fact]
    public void Field_checkers_detect_a_mismatch_between_two_different_npcs()
    {
        var world = BuildWorldWith100Npcs();
        var npcs = world.Npcs.Where(n => n.IsAlive).ToList();
        var dtoOfFirst = NpcInspectionQuery.Inspect(world, npcs[0].Id).Value!;
        var secondNpc = npcs[1];

        bool anyMismatch = FieldCheckers.Values.Any(checker => !checker(dtoOfFirst, secondNpc, world));

        Assert.True(anyMismatch, "checkers deveriam discriminar o DTO de um NPC comparado contra o estado de outro NPC");
    }
}
