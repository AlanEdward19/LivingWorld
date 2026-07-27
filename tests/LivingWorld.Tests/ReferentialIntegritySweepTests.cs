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
}
