using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Authoring;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Leitura de campos já públicos, consulta/apagar/implantar memória no log de
/// <see cref="Fact"/>, e alteração temporária de traço via
/// <see cref="WorldAuthoringCommands.RewritePersonality"/>. Nenhum poder nomeado.
/// </summary>
public sealed class MindMechanic : ExtraordinaryMechanic
{
    private const string ErasePrefix = "mind.erase-memory:";
    private const string ImplantPrefix = "mind.implant-memory:";

    public override string Prefix => "mind.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    // Parsers únicos (carga e invocação). Estado do mundo (alvo fantasma, Fact existente) fica em Prepare*.
    public static readonly ExtraordinaryPrimitive<bool> Read = Flag("mind.read");
    public static readonly ExtraordinaryPrimitive<bool> ReadMemory = Flag("mind.read-memory");
    public static readonly ExtraordinaryPrimitive<bool> Commune = Flag("mind.commune");

    public static readonly ExtraordinaryPrimitive<FactId> Erase = FactToken(ErasePrefix);
    public static readonly ExtraordinaryPrimitive<FactId> Implant = FactToken(ImplantPrefix);

    /// <summary>Parser único de <c>mind.alter-trait:&lt;traço&gt;:&lt;±n&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<(string Trait, int Amount)> AlterTrait = new(
        "mind.alter-trait:", "mind.alter-trait:<traço>:<±n>", true, "target",
        static token =>
        {
            const string alterPrefix = "mind.alter-trait:";
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Effects", allowSigned: true);
            if (!parsed.IsSuccess) return Result<(string, int)>.Fail(parsed.Error!);
            if (parsed.Value.Key.Length <= alterPrefix.Length
                || !TryCanonicalTrait(parsed.Value.Key[alterPrefix.Length..], out string traitName))
                return Result<(string, int)>.Fail($"Effects: alvo não suportado '{token}'");
            return Result<(string, int)>.Ok((traitName, parsed.Value.Amount));
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } =
        [Read, ReadMemory, Commune, Erase, Implant, AlterTrait];

    private static ExtraordinaryPrimitive<bool> Flag(string token) =>
        new(token, token, false, "target", PrimitiveSyntax.Flag(token));

    private static ExtraordinaryPrimitive<FactId> FactToken(string prefix) => new(
        prefix, $"{prefix}<memória>", false, "target",
        PrimitiveSyntax.From<FactId>(
            (string token, out FactId id) => TryParseFactToken(token, prefix, out id),
            static token => $"Effects: alvo não suportado '{token}'"));

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (Read.Accepts(declaration))
        {
            var target = ctx.Target;
            long tick = ctx.Tick.CurrentTick;
            return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
                ctx.Tick.LogEvent(WorldEventKind.ExtraordinaryEffectApplied, ReadPayload(target, tick), sourceSystem: "MindMechanic")));
        }

        if (ReadMemory.Accepts(declaration))
            return PrepareReadMemory(ctx, declaration);
        if (Commune.Accepts(declaration))
        {
            if (!ctx.Target.IsGhost)
                return Result<PreparedMutation?>.Fail("Effects: mind.commune exige alvo com IsGhost=true");
            return PrepareReadMemory(ctx, declaration);
        }
        if (declaration.StartsWith(ErasePrefix, StringComparison.Ordinal))
            return PrepareEraseMemory(ctx, declaration);
        if (declaration.StartsWith(ImplantPrefix, StringComparison.Ordinal))
            return PrepareImplantMemory(ctx, declaration);

        if (!declaration.StartsWith(AlterTrait.Head, StringComparison.Ordinal))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var parsed = AlterTrait.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation?>.Fail(parsed.Error!);
        var (traitName, amount) = parsed.Value;
        var targetNpc = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, resolution =>
        {
            int delta = ExtraordinaryMechanicSupport.ScaledAmount(amount, resolution);
            RememberPreAlteration(ctx.World, ctx.Carrier, targetNpc, traitName);
            var values = WithTraitDelta(targetNpc.Personality, traitName, delta);
            WorldAuthoringCommands.RewritePersonality(ctx.World, ctx.Tick, targetNpc.Id, values);
        }));
    }

    private static Result<PreparedMutation?> PrepareReadMemory(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var world = ctx.World;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
            ctx.Tick.LogEvent(WorldEventKind.ExtraordinaryEffectApplied, MemoryPayload(world, target), sourceSystem: "MindMechanic")));
    }

    private static Result<PreparedMutation?> PrepareEraseMemory(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var erase = Erase.Parse(declaration);
        if (!erase.IsSuccess)
            return Result<PreparedMutation?>.Fail(erase.Error!);
        var factId = erase.Value;

        var world = ctx.World;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var existing = CarrierStateOf(world, target);
            world.UpsertExtraordinaryCarrier(
                existing with { ForgottenFactIds = WithFact(existing.ForgottenFactIds, factId) });
        }));
    }

    private static Result<PreparedMutation?> PrepareImplantMemory(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var implant = Implant.Parse(declaration);
        if (!implant.IsSuccess)
            return Result<PreparedMutation?>.Fail(implant.Error!);
        var factId = implant.Value;
        if (ctx.World.FindFact(factId) is null)
            return Result<PreparedMutation?>.Fail($"Effects: Fact '{factId.Value}' ausente");

        var world = ctx.World;
        var target = ctx.Target;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var existing = CarrierStateOf(world, target);
            world.UpsertExtraordinaryCarrier(
                existing with { ImplantedFactIds = WithFact(existing.ImplantedFactIds, factId) });
        }));
    }

    private static ExtraordinaryCarrierState CarrierStateOf(WorldState world, Npc npc) =>
        world.CarrierOf(npc.Id)
        ?? new ExtraordinaryCarrierState(
            npc.Id, [], false, "dormant",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);

    private static IReadOnlySet<FactId> WithFact(IReadOnlySet<FactId>? previous, FactId factId)
    {
        var next = previous is null ? new HashSet<FactId>() : new HashSet<FactId>(previous);
        next.Add(factId);
        return next;
    }

    private static bool TryParseFactToken(string declaration, string prefix, out FactId factId)
    {
        factId = default;
        string numeric = declaration[prefix.Length..];
        if (!long.TryParse(numeric, out long value)
            || !numeric.Equals(value.ToString(), StringComparison.Ordinal))
            return false;
        factId = new FactId(value);
        return true;
    }

    private static string MemoryPayload(WorldState world, Npc target)
    {
        var state = world.CarrierOf(target.Id);
        var forgotten = state?.ForgottenFactIds;
        var implanted = state?.ImplantedFactIds;
        var ids = world.Facts
            .Where(fact =>
                (fact.Participants.Contains(target.Id)
                    || (implanted is not null && implanted.Contains(fact.Id)))
                && (forgotten is null || !forgotten.Contains(fact.Id)))
            .Select(fact => fact.Id.Value)
            .OrderBy(value => value)
            .ToList();
        return ids.Count == 0
            ? "mind.read-memory"
            : "mind.read-memory|" + string.Join('|', ids);
    }

    internal static void RevertPreAlterationTraits(
        WorldState world, TickContext ctx, Npc caster, IReadOnlyDictionary<string, double> traits)
    {
        var grouped = new Dictionary<long, Dictionary<string, double>>();
        foreach (var (key, stored) in traits)
        {
            var (npcId, traitName) = ParseStoredTrait(key, caster.Id.Value);
            if (!grouped.TryGetValue(npcId, out var byTrait))
            {
                byTrait = new Dictionary<string, double>(StringComparer.Ordinal);
                grouped[npcId] = byTrait;
            }
            if (!TryCanonicalTrait(traitName, out string canonical)) continue;
            byTrait[canonical] = stored;
        }

        foreach (var (npcId, byTrait) in grouped.OrderBy(pair => pair.Key))
        {
            var subject = world.FindNpc(new NpcId(npcId));
            if (subject is null || !subject.IsAlive) continue;
            var values = ToValues(subject.Personality);
            foreach (var (name, stored) in byTrait)
                values = ReplaceTrait(values, name, (int)Math.Clamp(stored, 0, 100));
            WorldAuthoringCommands.RewritePersonality(world, ctx, subject.Id, values);
        }
    }

    private static string ReadPayload(Npc target, long tick)
    {
        var p = target.Personality;
        return string.Join('|',
            "mind.read",
            $"extroversion={p.Extroversion}",
            $"agreeableness={p.Agreeableness}",
            $"conscientiousness={p.Conscientiousness}",
            $"emotionalStability={p.EmotionalStability}",
            $"openness={p.Openness}",
            $"ambition={p.Ambition}",
            $"loyalty={p.Loyalty}",
            $"altruism={p.Altruism}",
            $"impulsivity={p.Impulsivity}",
            $"riskAversion={p.RiskAversion}",
            $"hunger={target.HungerAt(tick)}",
            $"thirst={target.ThirstAt(tick)}",
            $"sleep={target.SleepAt(tick)}",
            $"social={target.SocialAt(tick)}",
            $"household={target.Household?.Value}",
            $"spouse={target.Spouse?.Value}");
    }

    private static void RememberPreAlteration(WorldState world, Npc caster, Npc target, string traitName)
    {
        var existing = world.CarrierOf(caster.Id);
        if (existing is null) return;
        var traits = existing.PreAlterationTraits is { } previous
            ? previous.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, double>(StringComparer.Ordinal);
        string key = $"{target.Id.Value}|{traitName}";
        if (!traits.ContainsKey(key))
            traits[key] = ReadTrait(target.Personality, traitName);
        world.UpsertExtraordinaryCarrier(existing with { PreAlterationTraits = traits });
    }

    private static (long NpcId, string TraitName) ParseStoredTrait(string key, long fallbackNpcId)
    {
        int separator = key.IndexOf('|');
        if (separator <= 0 || !long.TryParse(key[..separator], out long npcId))
            return (fallbackNpcId, key);
        return (npcId, key[(separator + 1)..]);
    }

    private static bool TryCanonicalTrait(string token, out string traitName)
    {
        string normalized = token.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        traitName = normalized switch
        {
            "extroversion" => "extroversion",
            "agreeableness" => "agreeableness",
            "conscientiousness" => "conscientiousness",
            "emotionalstability" => "emotional-stability",
            "openness" => "openness",
            "ambition" => "ambition",
            "loyalty" => "loyalty",
            "altruism" => "altruism",
            "impulsivity" => "impulsivity",
            "riskaversion" => "risk-aversion",
            _ => "",
        };
        return traitName.Length > 0;
    }

    private static int ReadTrait(Personality personality, string traitName) => traitName switch
    {
        "extroversion" => personality.Extroversion,
        "agreeableness" => personality.Agreeableness,
        "conscientiousness" => personality.Conscientiousness,
        "emotional-stability" => personality.EmotionalStability,
        "openness" => personality.Openness,
        "ambition" => personality.Ambition,
        "loyalty" => personality.Loyalty,
        "altruism" => personality.Altruism,
        "impulsivity" => personality.Impulsivity,
        "risk-aversion" => personality.RiskAversion,
        _ => 0,
    };

    private static PersonalityValues ToValues(Personality p) => new(
        p.Extroversion, p.Agreeableness, p.Conscientiousness, p.EmotionalStability,
        p.Openness, p.Ambition, p.Loyalty, p.Altruism, p.Impulsivity, p.RiskAversion);

    private static PersonalityValues WithTraitDelta(Personality personality, string traitName, int delta) =>
        ReplaceTrait(ToValues(personality), traitName, Math.Clamp(ReadTrait(personality, traitName) + delta, 0, 100));

    private static PersonalityValues ReplaceTrait(PersonalityValues values, string traitName, int value) =>
        traitName switch
        {
            "extroversion" => values with { Extroversion = value },
            "agreeableness" => values with { Agreeableness = value },
            "conscientiousness" => values with { Conscientiousness = value },
            "emotional-stability" => values with { EmotionalStability = value },
            "openness" => values with { Openness = value },
            "ambition" => values with { Ambition = value },
            "loyalty" => values with { Loyalty = value },
            "altruism" => values with { Altruism = value },
            "impulsivity" => values with { Impulsivity = value },
            "risk-aversion" => values with { RiskAversion = value },
            _ => values,
        };
}
