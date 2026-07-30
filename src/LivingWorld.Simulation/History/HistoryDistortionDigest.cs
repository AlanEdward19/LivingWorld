using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Digest determinístico da cadeia de distorção (Fase 10, HIST-05/HIST-06) — usado
/// pelos testes de dois processos.</summary>
public static class HistoryDistortionDigest
{
    public static string Compute(ulong seed, HistoryRules rules)
    {
        var (world, _) = ScenarioRunner.Create(seed, historyRules: rules);
        var npc = world.Npcs[0];
        var evt = new WorldEvent(5, WorldEventKind.Marriage, $"{npc.Id.Value}|2");
        if (!SignificanceCalculator.TryRecord(evt, world, rules).IsSuccess)
            throw new InvalidOperationException("fato de teste não gravado — ajuste o cenário");

        var fact = world.Facts[0];
        var report = new ReportState(
            world.NextReportIdAndAdvance(),
            fact.Id,
            npc.City,
            TransmissionMediumType.Song,
            HopCount: 0,
            Weight: fact.Significance,
            CreatedAtTick: 10,
            LastHopTick: 10);

        for (int i = 0; i < 5; i++)
            report = DistortionEngine.AdvanceHop(report, fact, rules, world.Rng, world, nowTick: 20 + i);

        var materialized = DistortionEngine.Materialize(report, fact, rules, world.Rng, world);
        return Hash($"{report.HopCount}:{materialized.DistanceFromFact:F6}:{materialized.DistortedMagnitude:F6}:{materialized.MoralizedNarrativeSeed}");
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
