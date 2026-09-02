using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Population;

/// <summary>Fase 6, T19 (SKILL-01/12): dois sensores de <c>Hash(world)</c> — (a) ganho aplicado a
/// um NPC já no teto do cenário não muda o hash (rápido, sem <c>Category=Scenario</c>); (b)
/// desligar <see cref="SkillsRules.Enabled"/> muda o hash depois de 10 anos, mesma seed (mesmo
/// mecanismo de <c>EconomyHashScenarioTests</c>/ECON-05).</summary>
public class SkillHashSensorTests
{
    // --- (a): ganho no teto não move o mundo ---

    [Fact]
    public void Hash_is_unchanged_when_practice_gain_is_applied_to_an_npc_already_at_the_cap()
    {
        var rules = ScenarioRunner.DefaultSkillsRules;
        var world = SkillScenarioHarness.CreateWorld(seed: 1);
        var npc = SkillScenarioHarness.MakeWorker(
            world, new ProfessionType(1), SkillScenarioHarness.SomeLocation, new RateGene(1.0),
            skills: SkillSet.Empty.WithGain(new SkillType(0), rules.Cap, rules.Cap));
        var workplace = SkillScenarioHarness.MakeWorkplace(world, new LocationType(1), SkillScenarioHarness.SomeLocation);
        SkillScenarioHarness.Hire(npc, workplace);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        string hashBefore = WorldSnapshot.CanonicalHash(world);
        new SkillPracticeSystem(rules).Tick(world, ctx);
        string hashAfter = WorldSnapshot.CanonicalHash(world);

        Assert.Equal(hashBefore, hashAfter);
    }

    // --- (b): desligar o sistema muda o mundo ---

    private const long TenYears = 10 * 12 * 30 * 24;

    [Fact]
    [Trait("Category", "Scenario")]
    public void Ten_year_hash_differs_between_skills_system_on_and_off_with_the_same_seed()
    {
        string hashOn = RunAndHash(seed: 42, ScenarioRunner.DefaultSkillsRules);
        string hashOff = RunAndHash(seed: 42, ScenarioRunner.DefaultSkillsRules with { Enabled = false });

        Assert.NotEqual(hashOn, hashOff);
    }

    private static string RunAndHash(ulong seed, SkillsRules skillsRules)
    {
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, seed, ScenarioRunner.InitialMap(seed, 20), ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules, economyRules: ScenarioRunner.DefaultEconomyRules,
            economyCatalog: ScenarioRunner.DefaultEconomyCatalog);
        PopulationSeeder.SeedInitial(world, 20, ScenarioRunner.DefaultCulture, ScenarioRunner.DefaultVillageLocation);

        var clock = new WorldClock(SystemsWith(skillsRules));
        clock.Run(world, TenYears);
        return WorldSnapshot.CanonicalHash(world);
    }

    /// <summary>Mesma lista de <see cref="ScenarioRunner.DefaultSystems"/>, só substituindo os 3
    /// sistemas que recebem <see cref="SkillsRules"/> por construtor — mesmo princípio de
    /// <c>EconomyScenarioHarness</c> (Fase 5): nenhuma mudança em <c>ScenarioRunner.cs</c> pra
    /// variar um parâmetro só de teste.</summary>
    private static IReadOnlyList<ISimulationSystem> SystemsWith(SkillsRules skillsRules) =>
        ScenarioRunner.DefaultSystems().Select(system => system.Name switch
        {
            SkillPracticeSystem.SystemName => new SkillPracticeSystem(skillsRules),
            SkillTeachingSystem.SystemName => new SkillTeachingSystem(skillsRules, ScenarioRunner.DefaultLifeStageRules),
            ProductionSystem.SystemName => new ProductionSystem(skillsRules),
            _ => system,
        }).ToList();
}
