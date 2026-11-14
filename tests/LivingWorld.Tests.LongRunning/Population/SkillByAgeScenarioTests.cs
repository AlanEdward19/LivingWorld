using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.LongRunning.Population;

/// <summary>AD-132 na vila padrão (5 seeds × 35 anos, todos os moradores medidos nasceram no mundo): ninguém com menos de 5
/// anos sabe ofício; o jovem de 14–16 (faixa: com nascimento anual uma coorte pode faltar) fica abaixo do que a casa pode ensinar (fração do teto) e abaixo do adulto de 30. Antes,
/// com "escola" desde o berço, a criança de 1 ano já tinha ~77 de 100 e aos 5 anos ~99.</summary>
public class SkillByAgeScenarioTests
{
    private const int Years = 35;

    private sealed record Curve(double Under5, double At15, double At30);

    private static Curve Measure(ulong seed)
    {
        var (world, clock) = ScenarioRunner.Create(seed);
        long start = world.CurrentDate.TotalHours;
        clock.Run(world, Years * world.Calendar.HoursPerYear);
        var rules = ScenarioRunner.DefaultSkillsRules;
        var skills = rules.WorkSkills();
        var now = world.CurrentDate;
        var born = world.Npcs.Where(n => n.IsAlive && n.BirthDate.TotalHours > start).ToList();
        double Mean(Func<int, bool> age) =>
            born.Where(n => age(n.AgeYears(now))).Select(n => skills.Max(s => n.Skills.Get(s))).DefaultIfEmpty(double.NaN).Average();
        return new Curve(Mean(a => a < 5), Mean(a => a is >= 14 and <= 16), Mean(a => a is >= 28 and <= 32));
    }

    [Fact, Trait("Category", "Scenario")]
    public void Small_children_know_no_trade_and_a_fifteen_year_old_is_well_below_a_thirty_year_old()
    {
        // Grau único dos cenários (ParallelSeeds, até 8): as 5 seeds numa onda só; com grau 4 a 5ª esperava uma corrida de 35 anos.
        var curves = LivingWorld.Tests.Shared.ParallelSeeds.Map(1, 5, Measure);
        double homeCeiling = ScenarioRunner.DefaultSkillsRules.Learning.HouseholdCeilingFraction * ScenarioRunner.DefaultSkillsRules.Cap;

        Assert.All(curves, c => Assert.True(c.Under5 == 0 && c.At15 < homeCeiling && c.At15 < c.At30 / 2,
            $"menos de 5: {c.Under5:F1}, 14–16 anos: {c.At15:F1}, 28–32 anos: {c.At30:F1}"));
    }
}
