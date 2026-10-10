using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Population.Archive;

namespace LivingWorld.Tests.Unit.Population.Archive;

public class ColdTierArchiveTests
{
    private static readonly WorldCalendar Calendar = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    [Fact]
    public void AllNpcSummaries_reflects_entries_written_via_RestoreNpcSummaries()
    {
        var archive = new ColdTierArchive();
        var entries = new Dictionary<long, ColdTierArchive.NpcSummary>
        {
            [1] = Sample(1, "Aldric"),
            [2] = Sample(2, "Brynn"),
        };

        archive.RestoreNpcSummaries(entries);

        Assert.Equal(2, archive.AllNpcSummaries.Count);
        Assert.Equal(entries[1], archive.AllNpcSummaries[1]);
        Assert.Equal(entries[2], archive.AllNpcSummaries[2]);
        Assert.Equal(entries[1], archive.Lookup(1));
    }

    [Fact]
    public void RestoreNpcSummaries_replaces_prior_entries_entirely()
    {
        var archive = new ColdTierArchive();
        archive.RestoreNpcSummaries(new Dictionary<long, ColdTierArchive.NpcSummary> { [1] = Sample(1, "Old") });

        archive.RestoreNpcSummaries(new Dictionary<long, ColdTierArchive.NpcSummary> { [2] = Sample(2, "New") });

        Assert.Null(archive.Lookup(1));
        Assert.NotNull(archive.Lookup(2));
        Assert.Single(archive.AllNpcSummaries);
    }

    private static ColdTierArchive.NpcSummary Sample(long id, string name) =>
        new(new NpcId(id), name, Sex.Male, WorldDate.Epoch(Calendar).AddYears(-40), null, new CultureId(1), new ProfessionType(2));
}
