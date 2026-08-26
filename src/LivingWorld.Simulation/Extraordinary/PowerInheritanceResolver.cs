using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Resultado dos dois rolls de herança de poder (EVO-10, EVO-15, EVO-16).
/// Descritores por caminho entram em T8–T10.</summary>
public enum PowerInheritanceOutcome
{
    Both,
    OneOf,
    Mixed
}

/// <summary>Decisão do resolver: se a herança ocorre e qual dos 3 caminhos.</summary>
public sealed record PowerInheritanceDecision(
    bool Occurred,
    PowerInheritanceOutcome? Outcome);

/// <summary>Roll 1 (ocorre?) + roll 2 (ambos / um só / mistura) — EVO-10/15/16.
/// Sem os dois pais portadores, nenhum roll executa (checagem O(1)).</summary>
public static class PowerInheritanceResolver
{
    public const string OccursSalt = "inheritance-occurs";
    public const string OutcomeSalt = "inheritance-outcome";

    /// <summary>Decide herança a partir de flags de portador (sem tocar descritores).</summary>
    public static PowerInheritanceDecision Decide(
        ulong worldSeed,
        NpcId childId,
        bool parentAIsCarrier,
        bool parentBIsCarrier,
        PowerInheritanceRules? rules = null)
    {
        if (!parentAIsCarrier || !parentBIsCarrier)
            return new PowerInheritanceDecision(Occurred: false, Outcome: null);

        var resolved = PowerInheritanceRules.Resolve(rules);
        double occursRoll = DeterministicChoice.InUnitInterval(worldSeed, childId, OccursSalt);
        if (occursRoll >= resolved.InheritanceChance)
            return new PowerInheritanceDecision(Occurred: false, Outcome: null);

        var outcome = ChooseOutcome(worldSeed, childId, resolved);
        return new PowerInheritanceDecision(Occurred: true, Outcome: outcome);
    }

    /// <summary>Decide usando <see cref="WorldState.ExtraordinaryCarriers"/> —
    /// portador = tem pelo menos um power id.</summary>
    public static PowerInheritanceDecision Decide(
        WorldState world,
        NpcId childId,
        NpcId parentAId,
        NpcId parentBId,
        PowerInheritanceRules? rules = null)
    {
        bool parentAIsCarrier = IsPowerCarrier(world, parentAId);
        bool parentBIsCarrier = IsPowerCarrier(world, parentBId);
        return Decide(world.Seed, childId, parentAIsCarrier, parentBIsCarrier, rules);
    }

    internal static bool IsPowerCarrier(WorldState world, NpcId npcId)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npcId);
        return carrier is not null && carrier.PowerIds.Count > 0;
    }

    internal static PowerInheritanceOutcome ChooseOutcome(
        ulong worldSeed,
        NpcId childId,
        PowerInheritanceRules rules)
    {
        double total = rules.BothWeight + rules.OneOfWeight + rules.MixedWeight;
        double roll = DeterministicChoice.InUnitInterval(worldSeed, childId, OutcomeSalt) * total;

        if (roll < rules.BothWeight)
            return PowerInheritanceOutcome.Both;

        if (roll < rules.BothWeight + rules.OneOfWeight)
            return PowerInheritanceOutcome.OneOf;

        return PowerInheritanceOutcome.Mixed;
    }

    /// <summary>EVO-11: filho recebe cópias dos descritores de A e B, completos e
    /// independentes — sem fusão nem alteração. Nova lista; records imutáveis.</summary>
    public static IReadOnlyList<PowerDescriptor> ApplyBoth(
        IReadOnlyList<PowerDescriptor> parentA,
        IReadOnlyList<PowerDescriptor> parentB)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        var result = new List<PowerDescriptor>(parentA.Count + parentB.Count);
        result.AddRange(parentA);
        result.AddRange(parentB);
        return result;
    }

    /// <summary>Decide + aplica caminho de descritores. Both → <see cref="ApplyBoth"/>;
    /// OneOf/Mixed ainda não implementados (T9/T10).</summary>
    public static IReadOnlyList<PowerDescriptor> ResolveDescriptors(
        ulong worldSeed,
        NpcId childId,
        bool parentAIsCarrier,
        bool parentBIsCarrier,
        IReadOnlyList<PowerDescriptor> parentADescriptors,
        IReadOnlyList<PowerDescriptor> parentBDescriptors,
        PowerInheritanceRules? rules = null)
    {
        var decision = Decide(
            worldSeed, childId, parentAIsCarrier, parentBIsCarrier, rules);
        return ApplyOutcome(decision, parentADescriptors, parentBDescriptors);
    }

    /// <summary>Decide a partir de portadores no mundo + aplica caminho Both (EVO-11).
    /// OneOf/Mixed → T9/T10.</summary>
    public static IReadOnlyList<PowerDescriptor> ResolveDescriptors(
        WorldState world,
        NpcId childId,
        NpcId parentAId,
        NpcId parentBId,
        PowerInheritanceRules? rules = null)
    {
        var decision = Decide(world, childId, parentAId, parentBId, rules);
        if (!decision.Occurred)
            return [];

        var parentA = LookupDescriptors(world, parentAId);
        var parentB = LookupDescriptors(world, parentBId);
        return ApplyOutcome(decision, parentA, parentB);
    }

    static IReadOnlyList<PowerDescriptor> ApplyOutcome(
        PowerInheritanceDecision decision,
        IReadOnlyList<PowerDescriptor> parentADescriptors,
        IReadOnlyList<PowerDescriptor> parentBDescriptors)
    {
        if (!decision.Occurred)
            return [];

        return decision.Outcome switch
        {
            PowerInheritanceOutcome.Both => ApplyBoth(parentADescriptors, parentBDescriptors),
            // T9
            PowerInheritanceOutcome.OneOf => throw new NotSupportedException(
                "OneOf inheritance outcome is not implemented yet (T9)."),
            // T10
            PowerInheritanceOutcome.Mixed => throw new NotSupportedException(
                "Mixed inheritance outcome is not implemented yet (T10)."),
            _ => [],
        };
    }

    static IReadOnlyList<PowerDescriptor> LookupDescriptors(WorldState world, NpcId npcId)
    {
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npcId);
        if (carrier is null || carrier.PowerIds.Count == 0)
            return [];

        var byId = world.Extraordinary.Descriptors.ToDictionary(
            d => d.Id, StringComparer.Ordinal);
        var list = new List<PowerDescriptor>(carrier.PowerIds.Count);
        foreach (var powerId in carrier.PowerIds)
        {
            if (byId.TryGetValue(powerId, out var descriptor))
                list.Add(descriptor);
        }

        return list;
    }
}
