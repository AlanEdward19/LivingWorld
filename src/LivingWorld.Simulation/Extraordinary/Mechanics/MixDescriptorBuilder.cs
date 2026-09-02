using System.Globalization;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Caminho "mistura" (EVO-13/14/21): recombina eixos Source/Effects/Costs/Conditions/
/// AcquisitionRules. Mesma chave de mecânica agrega magnitude (soma, sem teto); chave só de
/// um pai é incluída (listas CSV). Eixos singulares em conflito usam
/// <see cref="DeterministicChoice"/>. Resultado inválido no contrato 16.1 → <c>null</c>
/// (filho nasce sem poder). Mecânicas diferentes nunca geram erro de incompatibilidade.
/// </summary>
public static class MixDescriptorBuilder
{
    public const string SourceSalt = "mix-source";
    public const string ConditionSalt = "mix-condition";
    public const string ShellSalt = "mix-shell";
    public const string IdSalt = "mix-id";

    /// <summary>
    /// Produz um descritor novo a partir dos dois pais, ou <c>null</c> se a recombinação
    /// for inválida (args incompatíveis na mesma chave, ou tokens que falham o contrato
    /// Prepare/registry da 16.1).
    /// </summary>
    public static PowerDescriptor? Build(
        PowerDescriptor parentA,
        PowerDescriptor parentB,
        ulong seed,
        NpcId childId,
        IExtraordinaryMechanicRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(parentA);
        ArgumentNullException.ThrowIfNull(parentB);

        registry ??= ExtraordinaryMechanicRegistry.Default;

        if (!TryMixTokenAxis(parentA.Effects, parentB.Effects, seed, childId, "effects", out var effects))
            return null;
        if (!TryMixTokenAxis(parentA.Costs, parentB.Costs, seed, childId, "costs", out var costs))
            return null;
        if (!TryMixTokenAxis(
                parentA.AcquisitionRules, parentB.AcquisitionRules, seed, childId, "acquisition",
                out var acquisition))
            return null;

        string source = MixSingular(parentA.Source, parentB.Source, seed, childId, SourceSalt);
        string? condition = MixOptionalSingular(
            parentA.ManifestationCondition, parentB.ManifestationCondition, seed, childId, ConditionSalt);

        var shell = PreferA(seed, childId, ShellSalt) ? parentA : parentB;
        string id = NewId(parentA.Id, parentB.Id, seed, childId);

        var mixed = new PowerDescriptor(
            id,
            source,
            effects,
            shell.Mode,
            costs,
            shell.Reliability,
            shell.FailureModes,
            shell.IntrinsicVulnerabilities,
            shell.Manifestations,
            acquisition,
            shell.Appearance,
            shell.NeedSubstitution,
            shell.SenescenceRateMultiplier,
            condition,
            Stages: null);

        return PassesPrepareContract(mixed, registry) ? mixed : null;
    }

    /// <summary>
    /// Contrato leve alinhado ao Prepare da 16.1: Mode/Reliability estruturais + cada
    /// efeito/custo resolve no registro e passa em <see cref="IExtraordinaryMechanic.PrepareEffect"/>
    /// / <see cref="IExtraordinaryMechanic.PrepareCost"/> (sem aplicar mutações).
    /// </summary>
    public static bool PassesPrepareContract(
        PowerDescriptor descriptor, IExtraordinaryMechanicRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        registry ??= ExtraordinaryMechanicRegistry.Default;

        if (descriptor.Mode is not ("Passive" or "Active" or "Triggered" or "Conditional"))
            return false;
        if (descriptor.Reliability is not ("Guaranteed" or "ResolutionCheck"))
            return false;
        if (descriptor.FailureModes.Count > 0 && descriptor.Reliability != "ResolutionCheck")
            return false;
        if (descriptor.Effects.Count == 0)
            return false;
        if (descriptor.Effects.Any(string.IsNullOrWhiteSpace))
            return false;

        var world = CreateValidationWorld(descriptor);
        var carrier = world.Npcs[0];
        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        var invocation = new ExtraordinaryInvocation(
            1, carrier.Id, descriptor.Id, carrier.Id,
            Origin: ExtraordinaryInvocationOrigin.Authored);
        var mechanicCtx = new ExtraordinaryMechanicContext(
            world, ctx, invocation, carrier, carrier, ExtraordinaryMechanicKind.Effect);

        foreach (var effect in descriptor.Effects)
        {
            var mechanic = registry.Resolve(effect);
            if (mechanic is null)
                return false;
            var prepared = mechanic.PrepareEffect(mechanicCtx, effect);
            if (!prepared.IsSuccess)
                return false;
        }

        var costCtx = mechanicCtx with { Kind = ExtraordinaryMechanicKind.Cost };
        foreach (var cost in descriptor.Costs)
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(cost, "Costs", allowSigned: false);
            if (!parsed.IsSuccess)
                return false;
            var mechanic = registry.Resolve(parsed.Value.Key);
            if (mechanic is null)
                return false;
            var prepared = mechanic.PrepareCost(costCtx, cost, parsed.Value.Amount);
            if (!prepared.IsSuccess)
                return false;
        }

        return true;
    }

    static bool TryMixTokenAxis(
        IReadOnlyList<string> a,
        IReadOnlyList<string> b,
        ulong seed,
        NpcId childId,
        string axis,
        out IReadOnlyList<string> mixed)
    {
        mixed = [];
        var byKey = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(IReadOnlyList<string> tokens)
        {
            foreach (var token in tokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;
                string key = MechanicKey(token);
                if (!byKey.TryGetValue(key, out var list))
                {
                    list = [];
                    byKey[key] = list;
                }

                list.Add(token);
            }
        }

        Add(a);
        Add(b);

        var result = new List<string>(byKey.Count);
        foreach (var (key, tokens) in byKey)
        {
            if (!TryResolveKeyTokens(tokens, seed, childId, axis, key, out string? chosen) || chosen is null)
                return false;
            result.Add(chosen);
        }

        mixed = result;
        return true;
    }

    static bool TryResolveKeyTokens(
        IReadOnlyList<string> tokens,
        ulong seed,
        NpcId childId,
        string axis,
        string key,
        out string? chosen)
    {
        chosen = null;
        if (tokens.Count == 0)
            return true;

        var distinct = tokens.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 1)
        {
            chosen = distinct[0];
            return true;
        }

        // Mesma chave, tokens distintos: tentar agregar magnitudes; senão conflito → null.
        if (TryAggregateMagnitudes(key, distinct, out string? aggregated))
        {
            chosen = aggregated;
            return true;
        }

        // Args incompatíveis na mesma chave = erro de configuração de cenário (design).
        // Não "escolhe" — descarta a mistura inteira.
        _ = seed;
        _ = childId;
        _ = axis;
        return false;
    }

    static bool TryAggregateMagnitudes(string key, IReadOnlyList<string> tokens, out string? aggregated)
    {
        aggregated = null;
        long? intSum = 0;
        double? doubleSum = 0;
        bool allInt = true;
        bool allDouble = true;

        foreach (var token in tokens)
        {
            if (!TrySplitKeyArgs(token, out string tokenKey, out string? args)
                || !string.Equals(tokenKey, key, StringComparison.Ordinal)
                || args is null)
            {
                allInt = false;
                allDouble = false;
                break;
            }

            if (allInt
                && int.TryParse(args, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
                && args.Equals(i.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                intSum = checked(intSum!.Value + i);
            }
            else
            {
                allInt = false;
            }

            if (allDouble
                && double.TryParse(args, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                && !double.IsNaN(d) && !double.IsInfinity(d)
                && args.IndexOf(':', StringComparison.Ordinal) < 0)
            {
                doubleSum = doubleSum!.Value + d;
            }
            else
            {
                allDouble = false;
            }
        }

        if (allInt && intSum is long sumI)
        {
            aggregated = $"{key}:{sumI.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        if (allDouble && doubleSum is double sumD)
        {
            aggregated = $"{key}:{sumD.ToString(CultureInfo.InvariantCulture)}";
            return true;
        }

        return false;
    }

    static string MechanicKey(string token)
    {
        TrySplitKeyArgs(token, out string key, out _);
        return key;
    }

    static bool TrySplitKeyArgs(string token, out string key, out string? args)
    {
        int separator = token.IndexOf(':');
        if (separator <= 0)
        {
            key = token;
            args = null;
            return true;
        }

        key = token[..separator];
        args = token[(separator + 1)..];
        return key.Length > 0;
    }

    static string MixSingular(string a, string b, ulong seed, NpcId childId, string salt)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return a;
        return PreferA(seed, childId, salt) ? a : b;
    }

    static string? MixOptionalSingular(
        string? a, string? b, ulong seed, NpcId childId, string salt)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
            return a;
        if (a is null)
            return b;
        if (b is null)
            return a;
        return PreferA(seed, childId, salt) ? a : b;
    }

    static bool PreferA(ulong seed, NpcId childId, string salt) =>
        DeterministicChoice.InUnitInterval(seed, childId, salt) < 0.5;

    static string NewId(string parentAId, string parentBId, ulong seed, NpcId childId)
    {
        // Id determinístico a partir dos pais + seed + filho (nunca Guid/Random).
        double mix = DeterministicChoice.InUnitInterval(seed, childId, $"{IdSalt}:{parentAId}:{parentBId}");
        ulong bits = (ulong)(mix * (1UL << 53));
        bits ^= StableHash.Mix(childId.Value);
        bits ^= StableHash.Mix(unchecked((long)seed));
        return $"mixed-{parentAId}-{parentBId}-{bits:x16}";
    }

    static WorldState CreateValidationWorld(PowerDescriptor descriptor)
    {
        var state = new ExtraordinaryCarrierState(
            new NpcId(1), [descriptor.Id], true, "active",
            new ExtraordinaryAppearanceState(1, "", ""), null, 1);
        var world = new WorldState(
            ScenarioRunner.DefaultCalendar, 1, ScenarioRunner.DefaultMap(1),
            ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            ScenarioRunner.DefaultNeedsRules, ScenarioRunner.DefaultActionCatalog,
            ScenarioRunner.DefaultLifeStageRules,
            extraordinary: new ExtraordinaryScenarioData(true, [descriptor]),
            extraordinaryCarriers: [state]);
        var npc = new Npc(
            new NpcId(1), "validator", Sex.Male,
            WorldDate.Epoch(world.Calendar).AddYears(-20),
            ScenarioRunner.DefaultCulture, new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!,
            profession: ProfessionType.None, currentLocation: new CellCoord(0, 0));
        world.AddNpc(npc);
        return world;
    }
}
