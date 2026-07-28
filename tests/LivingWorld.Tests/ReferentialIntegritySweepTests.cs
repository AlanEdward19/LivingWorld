using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests;

/// <summary>Task 12: sweep genérico limpo ao fim de todo cenário, e reprova se algum tipo de id
/// do assembly Domain não tiver resolver registrado (R5 — nunca "auditoria de código").</summary>
public class ReferentialIntegritySweepTests
{
    private const long TenYearsInHours = 10 * 12 * 30 * 24;

    [Fact]
    public void Every_id_type_in_the_domain_assembly_has_a_registered_resolver()
    {
        foreach (var idType in ReferentialIntegritySweep.AllIdTypesInDomainAssembly())
            Assert.Contains(idType, ReferentialIntegritySweep.RegisteredIdTypes);
    }

    [Fact]
    public void Default_scenario_after_10_years_has_no_referential_violations()
    {
        var (world, clock) = ScenarioRunner.Create(seed: 42);
        clock.Run(world, TenYearsInHours);

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.Empty(violations);
    }

    // Sensor de mutação (R5): prova que o sweep de fato detecta uma referência pendurada, em vez
    // de sempre devolver "limpo" sem medir nada.
    [Fact]
    public void A_household_member_pointing_to_a_nonexistent_npc_is_flagged()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 5);
        var household = world.Households.First();
        var danglingMember = new NpcId(999_999);
        household.AddMember(danglingMember);

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.Contains(violations, v => v.Contains(danglingMember.Value.ToString()) && v.Contains(nameof(NpcId)));
    }

    // Fase 5 (T11): mesmo sensor de mutação, agora sobre Npc.Employer/WorkplaceId.
    [Fact]
    public void An_npc_employer_pointing_to_a_nonexistent_workplace_is_flagged()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 5);
        var npc = world.Npcs.First();
        var danglingWorkplace = new WorkplaceId(999_999);
        npc.Hire(danglingWorkplace);

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.Contains(violations, v => v.Contains(danglingWorkplace.Value.ToString()) && v.Contains(nameof(WorkplaceId)));
    }

    // Fase 8 (T6): mesmo sensor de mutação, agora sobre Npc.City/CityId — o sweep resolve contra
    // world.Cities de verdade (não mais o placeholder vazio de T4).
    [Fact]
    public void An_npc_city_pointing_to_a_nonexistent_city_is_flagged()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 5);
        var npc = world.Npcs.First();
        var danglingCity = new CityId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        npc.JoinCity(danglingCity);

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.Contains(violations, v => v.Contains(danglingCity.Value.ToString()) && v.Contains(nameof(CityId)));
    }

    [Fact]
    public void An_npc_assigned_to_a_real_city_is_not_flagged()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 5);
        var npc = world.Npcs.First();
        var city = new City(
            world.NextCityId(), npc.CurrentLocation, foundedAtTick: 0, foundedFromCityId: null,
            aggregatePool: AggregatePopulationPool.Empty);
        world.AddCity(city);
        npc.JoinCity(city.Id);

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.DoesNotContain(violations, v => v.Contains(nameof(CityId)));
    }

    // Fase 8 (T6): BuildingId ganha resolver real a partir de world.Buildings (T5 já criou a
    // coleção) — mesmo sensor de mutação.
    [Fact]
    public void A_building_pointing_to_a_nonexistent_city_is_flagged()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 5);
        var danglingCity = new CityId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), danglingCity, buildingTypeId: 1, completedAtTick: 0));

        var violations = ReferentialIntegritySweep.Check(world);

        Assert.Contains(violations, v => v.Contains(danglingCity.Value.ToString()) && v.Contains(nameof(CityId)));
    }
}
