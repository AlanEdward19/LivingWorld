using LivingWorld.Tests.Shared;

namespace LivingWorld.Tests.LongRunning;

public class CatalogHistoryDeterminismTests
{
    [Fact]
    public void Catalog_history_is_identical_across_processes_and_diverges_for_another_seed()
    {
        string first = WorkersProcess.RunDigest("catalog-history-hash 91 25920");
        string second = WorkersProcess.RunDigest("catalog-history-hash 91 25920");
        Assert.Equal(first, second);
        var parts = first.Split(';');
        Assert.Equal(64, parts[0].Length);
        Assert.True(int.Parse(parts[1]) > 0, "no facts: history was not exercised");
        Assert.True(int.Parse(parts[2]) > 0, "no reports: witness conversion was not exercised");
        Assert.NotEqual(parts[0], WorkersProcess.RunDigest("catalog-history-hash 92 25920").Split(';')[0]);
    }
}
