using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Operadores de distorção determinísticos (Fase 10, HIST-05/HIST-06) — dispatch
/// explícito por enum, RNG derivado de <c>(ReportId, hop)</c>.</summary>
public static class DistortionEngine
{
    /// <summary>Probe opcional para testes — lança se algum caminho invocar LLM durante
    /// distorção (HIST-06 AC3).</summary>
    public static Action? LlmInvocationProbe;

    private static readonly IReadOnlyDictionary<DistortionOperator, double> DistanceDeltaByOperator =
        new Dictionary<DistortionOperator, double>
        {
            [DistortionOperator.AttributionSwap] = 0.12,
            [DistortionOperator.MagnitudeInflation] = 0.10,
            [DistortionOperator.TemporalCompression] = 0.08,
            [DistortionOperator.CausalLoss] = 0.15,
            [DistortionOperator.Moralization] = 0.11,
            [DistortionOperator.Anachronism] = 0.09,
            [DistortionOperator.ConvenientOmission] = 0.13,
            [DistortionOperator.CharacterMerge] = 0.14,
        };

    public static DistortedPayload FromFact(Fact fact) =>
        new(
            fact.Participants.ToList(),
            fact.Significance,
            fact.Tick,
            fact.Payload,
            MoralSeed: "",
            DistanceFromFact: 0);

    public static DistortedPayload Apply(
        DistortionOperator op,
        DistortedPayload input,
        WorldRng rng,
        WorldState? world = null)
    {
        LlmInvocationProbe?.Invoke();

        double delta = DistanceDeltaByOperator[op];
        var next = op switch
        {
            DistortionOperator.AttributionSwap => ApplyAttributionSwap(input, rng),
            DistortionOperator.MagnitudeInflation => ApplyMagnitudeInflation(input, rng),
            DistortionOperator.TemporalCompression => ApplyTemporalCompression(input, rng),
            DistortionOperator.CausalLoss => ApplyCausalLoss(input),
            DistortionOperator.Moralization => ApplyMoralization(input, rng),
            DistortionOperator.Anachronism => ApplyAnachronism(input, rng, world),
            DistortionOperator.ConvenientOmission => ApplyConvenientOmission(input, rng),
            DistortionOperator.CharacterMerge => ApplyCharacterMerge(input, rng),
            _ => input,
        };

        return next with { DistanceFromFact = input.DistanceFromFact + delta };
    }

    public static ReportState AdvanceHop(
        ReportState current,
        Fact origin,
        HistoryRules rules,
        WorldRngRegistry rngRegistry,
        WorldState world,
        long nowTick)
    {
        if (!rules.MediumFidelityByType.TryGetValue(current.Medium, out var medium))
            return current with { HopCount = current.HopCount + 1, LastHopTick = nowTick };

        int nextHop = current.HopCount + 1;
        if (nextHop > medium.ReachHops)
            return current with { LastHopTick = nowTick };

        var before = Materialize(current with { HopCount = current.HopCount }, origin, rules, rngRegistry, world);
        var after = Materialize(current with { HopCount = nextHop }, origin, rules, rngRegistry, world);
        if (after.DistanceFromFact + 1e-9 < before.DistanceFromFact)
            throw new InvalidOperationException("distance_from_fact_decreased");

        return current with { HopCount = nextHop, LastHopTick = nowTick };
    }

    public static DistortedReport Materialize(
        ReportState report,
        Fact origin,
        HistoryRules rules,
        WorldRngRegistry rngRegistry,
        WorldState world)
    {
        var payload = FromFact(origin);
        double previousDistance = 0;

        for (int hop = 1; hop <= report.HopCount; hop++)
        {
            payload = ApplyHopOperators(payload, report, hop, rules, rngRegistry, world);
            if (payload.DistanceFromFact + 1e-9 < previousDistance)
                throw new InvalidOperationException("distance_from_fact_decreased");
            previousDistance = payload.DistanceFromFact;
        }

        return new DistortedReport(
            report.Id,
            payload.Participants,
            payload.Magnitude,
            payload.Tick,
            payload.MoralSeed,
            payload.DistanceFromFact);
    }

    public static double DistanceFromFact(
        ReportState report,
        Fact origin,
        HistoryRules rules,
        WorldRngRegistry rngRegistry,
        WorldState world) =>
        Materialize(report, origin, rules, rngRegistry, world).DistanceFromFact;

    private static DistortedPayload ApplyHopOperators(
        DistortedPayload payload,
        ReportState report,
        int hop,
        HistoryRules rules,
        WorldRngRegistry rngRegistry,
        WorldState world)
    {
        if (!rules.MediumFidelityByType.TryGetValue(report.Medium, out var medium))
            return payload;

        var rng = rngRegistry.StreamFor("history-distortion", StreamKey(report.Id, hop));
        if (rng.NextDouble() >= medium.DistortionRatePerHop)
            return payload;

        var op = SelectOperator(rules, rng);
        return Apply(op, payload, rng, world);
    }

    private static DistortionOperator SelectOperator(HistoryRules rules, WorldRng rng)
    {
        double roll = rng.NextDouble();
        double cumulative = 0;
        foreach (var op in Enum.GetValues<DistortionOperator>().OrderBy(o => o))
        {
            cumulative += rules.OperatorProbability.GetValueOrDefault(op, 0);
            if (roll <= cumulative)
                return op;
        }

        return DistortionOperator.ConvenientOmission;
    }

    private static long StreamKey(ReportId reportId, int hop) =>
        unchecked(reportId.Value * 1009L + hop);

    private static DistortedPayload ApplyAttributionSwap(DistortedPayload input, WorldRng rng)
    {
        if (input.Participants.Count == 0)
            return input;

        var participants = input.Participants.ToList();
        if (participants.Count >= 2)
        {
            (participants[0], participants[1]) = (participants[1], participants[0]);
            return input with { Participants = participants };
        }

        long famousId = participants[0].Value + 1 + NextInt(rng, 1, 5);
        participants[0] = new NpcId(famousId);
        return input with { Participants = participants };
    }

    private static DistortedPayload ApplyMagnitudeInflation(DistortedPayload input, WorldRng rng)
    {
        double factor = 1.5 + rng.NextDouble() * 1.5;
        return input with { Magnitude = Math.Min(1.0, input.Magnitude * factor) };
    }

    private static DistortedPayload ApplyTemporalCompression(DistortedPayload input, WorldRng rng)
    {
        long shift = NextInt(rng, 1, 24);
        return input with { Tick = Math.Max(0, input.Tick - shift) };
    }

    private static DistortedPayload ApplyCausalLoss(DistortedPayload input) =>
        input with { Payload = StripCausalSegment(input.Payload) };

    private static DistortedPayload ApplyMoralization(DistortedPayload input, WorldRng rng)
    {
        string tag = rng.NextDouble() < 0.5 ? "honor" : "shame";
        string moral = string.IsNullOrEmpty(input.MoralSeed) ? tag : $"{input.MoralSeed}|{tag}";
        return input with { MoralSeed = moral };
    }

    private static DistortedPayload ApplyAnachronism(DistortedPayload input, WorldRng rng, WorldState? world)
    {
        long shift = NextInt(rng, 1, 48);
        long now = world?.CurrentDate.TotalHours ?? input.Tick;
        return input with { Tick = Math.Min(now, input.Tick + shift) };
    }

    private static DistortedPayload ApplyConvenientOmission(DistortedPayload input, WorldRng rng)
    {
        if (input.Participants.Count <= 1)
            return input;

        var participants = input.Participants.ToList();
        participants.RemoveAt(NextInt(rng, 0, participants.Count));
        return input with { Participants = participants };
    }

    private static DistortedPayload ApplyCharacterMerge(DistortedPayload input, WorldRng rng)
    {
        if (input.Participants.Count <= 1)
            return input;

        var participants = input.Participants.ToList();
        int mergeIndex = NextInt(rng, 1, participants.Count);
        participants.RemoveAt(mergeIndex);
        return input with { Participants = participants };
    }

    private static string StripCausalSegment(string payload)
    {
        int bar = payload.IndexOf('|');
        return bar >= 0 ? payload[..bar] : payload;
    }

    private static int NextInt(WorldRng rng, int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (int)(rng.NextDouble() * (maxExclusive - minInclusive));
    }
}
