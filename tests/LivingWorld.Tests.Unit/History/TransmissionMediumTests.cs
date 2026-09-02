using LivingWorld.Domain.History;
using LivingWorld.Domain.History.Distortion;

namespace LivingWorld.Tests.Unit.History;

/// <summary>Fase 10, T6: <see cref="TransmissionMediumType"/> + <see cref="MediumFidelity"/> (HIST-08).</summary>
public class TransmissionMediumTests
{
    [Theory]
    [InlineData(TransmissionMediumType.LivingMemory)]
    [InlineData(TransmissionMediumType.OralTradition)]
    [InlineData(TransmissionMediumType.Song)]
    [InlineData(TransmissionMediumType.Book)]
    [InlineData(TransmissionMediumType.Monument)]
    public void Each_medium_has_fidelity_parameters_in_default_rules(TransmissionMediumType medium)
    {
        Assert.True(HistoryRules.Default.MediumFidelityByType.ContainsKey(medium));
        var fidelity = HistoryRules.Default.MediumFidelityByType[medium];
        Assert.InRange(fidelity.DistortionRatePerHop, 0, 1);
        Assert.True(fidelity.ReachHops >= 0);
    }

    [Fact]
    public void Distortion_rate_order_matches_historical_memory_doc()
    {
        var byType = HistoryRules.Default.MediumFidelityByType;
        double oral = byType[TransmissionMediumType.OralTradition].DistortionRatePerHop;
        double song = byType[TransmissionMediumType.Song].DistortionRatePerHop;
        double book = byType[TransmissionMediumType.Book].DistortionRatePerHop;
        double monument = byType[TransmissionMediumType.Monument].DistortionRatePerHop;

        Assert.True(oral > song);
        Assert.True(song > book);
        Assert.True(book > monument);
    }

    [Fact]
    public void Reach_order_matches_historical_memory_doc()
    {
        var byType = HistoryRules.Default.MediumFidelityByType;
        int book = byType[TransmissionMediumType.Book].ReachHops;
        int monument = byType[TransmissionMediumType.Monument].ReachHops;

        Assert.True(monument > book);
    }
}
