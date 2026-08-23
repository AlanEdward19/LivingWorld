using System.Reflection;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.History;

/// <summary>Fase 10, T7: <see cref="ReportState"/> + <see cref="ReportId"/> (HIST-01 AC4).</summary>
public class ReportStateTests
{
    [Fact]
    public void ReportState_creation_preserves_metadata_fields()
    {
        var cityId = new CityId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var report = new ReportState(
            new ReportId(1),
            new FactId(2),
            cityId,
            TransmissionMediumType.OralTradition,
            HopCount: 0,
            Weight: 0.75,
            CreatedAtTick: 10,
            LastHopTick: 10);

        Assert.Equal(new ReportId(1), report.Id);
        Assert.Equal(new FactId(2), report.OriginFactId);
        Assert.Equal(cityId, report.CommunityId);
        Assert.Equal(TransmissionMediumType.OralTradition, report.Medium);
        Assert.Equal(0, report.HopCount);
        Assert.Equal(0.75, report.Weight);
    }

    [Fact]
    public void ReportState_exposes_no_mutation_methods_or_setters()
    {
        Assert.True(typeof(ReportState).IsSealed);
        Assert.Null(typeof(ReportState).GetMethod("Update"));
        Assert.Null(typeof(ReportState).GetMethod("Mutate"));

        foreach (var prop in typeof(ReportState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var set = prop.SetMethod;
            if (set is null || !set.IsPublic)
                continue;
            Assert.Contains(
                "IsExternalInit",
                set.ReturnParameter.GetRequiredCustomModifiers().Select(t => t.Name));
        }
    }

    [Fact]
    public void WorldState_assigns_monotonic_report_ids()
    {
        var (world, _) = ScenarioRunner.Create(1);
        Assert.Equal(new ReportId(0), world.NextReportIdAndAdvance());
        Assert.Equal(new ReportId(1), world.NextReportIdAndAdvance());
    }
}
