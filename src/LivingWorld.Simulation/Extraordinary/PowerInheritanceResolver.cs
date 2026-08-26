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
}
