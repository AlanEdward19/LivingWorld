using LivingWorld.Tests.Baselines;

namespace LivingWorld.Tests;

public class BaselineFixtureTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lw-baseline-" + Guid.NewGuid());
    private sealed record Sample(int Roll, string Outcome);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_baseline_fails_instead_of_being_generated_silently()
    {
        var actual = new Dictionary<int, Sample> { [1] = new(10, "Success") };

        Assert.Throws<BaselineMissingException>(() => BaselineFixture.AssertMatches(_dir, "missing", actual));
        Assert.False(File.Exists(Path.Combine(_dir, "missing.json")));
    }

    [Fact]
    public void Tampered_baseline_reports_seed_and_field()
    {
        var recorded = new Dictionary<int, Sample> { [1] = new(10, "Success"), [2] = new(7, "Failure") };
        BaselineFixture.Record(_dir, "tamper", recorded);

        var path = Path.Combine(_dir, "tamper.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Roll\": 10", "\"Roll\": 999"));

        var ex = Assert.Throws<BaselineMismatchException>(() => BaselineFixture.AssertMatches(_dir, "tamper", recorded));
        Assert.Contains("seed 1", ex.Message);
        Assert.Contains("campo Roll", ex.Message);
        Assert.Contains("999", ex.Message);
    }

    [Fact]
    public void Matching_baseline_passes()
    {
        var recorded = new Dictionary<int, Sample> { [1] = new(10, "Success"), [2] = new(7, "Failure") };
        BaselineFixture.Record(_dir, "ok", recorded);

        BaselineFixture.AssertMatches(_dir, "ok", recorded);
    }
}
