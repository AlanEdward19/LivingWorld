using System.Security.Cryptography;
using System.Text;
using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Digest determinístico de correção compensatória (Fase 10, HIST-24) — usado pelos
/// testes de dois processos.</summary>
public static class HistoryCorrectionDigest
{
    public static string Compute(ulong seed)
    {
        var (world, _) = ScenarioRunner.Create(seed, historyRules: HistoryRules.Default);
        var npc = world.Npcs[0];
        var original = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Birth, [npc.Id], npc.City, 0.9, "wrong");
        world.AddFact(original);
        CompensatingCorrectionOperations.Apply(
            world,
            original.Id,
            tick: 20,
            WorldEventKind.Birth,
            [npc.Id],
            npc.City,
            0.9,
            "correct",
            "fix");
        var line = CompensatingCorrectionOperations.GetFactLine(world, original.Id);
        var content = string.Join(';', line.OrderBy(e => e.Fact.Id.Value).Select(e => $"{e.Role}:{e.Fact.Id.Value}:{e.Fact.Payload}"));
        return Hash(content);
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
