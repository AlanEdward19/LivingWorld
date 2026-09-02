using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Aplica correção compensatória ao esqueleto (Fase 10, HIST-24) — append-only.</summary>
public static class CompensatingCorrectionOperations
{
    public static Result<CompensatingCorrection> Apply(
        WorldState world,
        FactId correctsFactId,
        long tick,
        WorldEventKind kind,
        IReadOnlyList<NpcId> participants,
        CityId? location,
        double significance,
        string correctedPayload,
        string reason)
    {
        if (world.FindFact(correctsFactId) is null)
            return Result<CompensatingCorrection>.Fail("original_not_found");

        if (string.IsNullOrWhiteSpace(reason))
            return Result<CompensatingCorrection>.Fail("reason_required");

        var correctionFact = new Fact(
            world.NextFactIdAndAdvance(),
            tick,
            WorldEventKind.CompensatingCorrection,
            participants.ToList(),
            location,
            significance,
            FormatPayload(correctsFactId, correctedPayload, reason));
        world.AddFact(correctionFact);

        return Result<CompensatingCorrection>.Ok(
            new CompensatingCorrection(correctsFactId, correctionFact.Id, tick, reason));
    }

    public static IReadOnlyList<MarkedFactEntry> GetFactLine(WorldState world, FactId factId)
    {
        var original = world.FindFact(factId);
        if (original is null)
            return [];

        if (original.Kind == WorldEventKind.CompensatingCorrection
            && TryParsePayload(original.Payload, out var correctsId, out _, out _))
        {
            var correctedOriginal = world.FindFact(correctsId);
            var entries = new List<MarkedFactEntry> { new(original, FactLineRole.Correction) };
            if (correctedOriginal is not null)
                entries.Add(new MarkedFactEntry(correctedOriginal, FactLineRole.Original));
            return entries.OrderBy(e => e.Fact.Id.Value).ToList();
        }

        var line = new List<MarkedFactEntry> { new(original, FactLineRole.Original) };
        foreach (var correction in world.Facts.OrderBy(f => f.Id.Value))
        {
            if (correction.Kind != WorldEventKind.CompensatingCorrection)
                continue;
            if (TryParsePayload(correction.Payload, out var corrects, out _, out _)
                && corrects == factId)
                line.Add(new MarkedFactEntry(correction, FactLineRole.Correction));
        }
        return line;
    }

    internal static string FormatPayload(FactId correctsFactId, string correctedPayload, string reason) =>
        $"{correctsFactId.Value}|{correctedPayload}|{reason}";

    internal static bool TryParsePayload(string payload, out FactId correctsFactId, out string correctedPayload, out string reason)
    {
        correctsFactId = default;
        correctedPayload = "";
        reason = "";
        var parts = payload.Split('|', 3);
        if (parts.Length < 3 || !long.TryParse(parts[0], out var id))
            return false;
        correctsFactId = new FactId(id);
        correctedPayload = parts[1];
        reason = parts[2];
        return true;
    }
}
