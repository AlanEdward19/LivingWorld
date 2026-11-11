using LivingWorld.Simulation.Observation;

namespace LivingWorld.Tests.Unit.Population.Lifecycle;

/// <summary>AD-130, cenário padrão de código em horizonte curto: nascimentos espalhados pelo ano e mortes por idade fora do
/// aniversário. Antes, toda concepção saía no tick de ano-novo (todo parto no mesmo dia) e a morte caía no aniversário.</summary>
[Trait("Category", "Scenario")]
public class DemographyCalendarScenarioTests
{
    private static readonly Lazy<DemographyReport.SeedResult[]> Runs = new(() =>
        Enumerable.Range(1, 3).AsParallel().AsOrdered().Select(seed => DemographyReport.Run((ulong)seed, years: 10, warmupYears: 0)).ToArray());

    [Fact]
    public void Births_fall_in_every_month_and_no_month_holds_a_quarter_of_them()
    {
        var byMonth = new long[12];
        foreach (var run in Runs.Value)
            for (int m = 0; m < 12; m++) byMonth[m] += run.Tally.BirthsByMonth[m];
        long total = byMonth.Sum();

        Assert.True(byMonth.All(b => b > 0) && byMonth.Max() * 4 < total, string.Join(",", byMonth));
    }

    [Fact]
    public void Deaths_by_age_almost_never_fall_on_the_birthday()
    {
        long deaths = Runs.Value.Sum(r => r.Tally.Deaths);
        long onBirthday = Runs.Value.Sum(r => r.Tally.DeathsOnBirthday);

        Assert.True(deaths > 0 && onBirthday * 20 < deaths, $"{onBirthday} de {deaths} mortes no aniversário");
    }

    [Fact]
    public void The_village_survives_ten_years_with_period_mortality() =>
        Assert.All(Runs.Value, run => Assert.True(run.FinalAlive > 0));
}
