using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public enum ExtraordinaryMechanicKind
{
    Effect,
    Cost,
}

public sealed record ExtraordinaryMechanicContext(
    WorldState World,
    TickContext Tick,
    ExtraordinaryInvocation Invocation,
    Npc Carrier,
    Npc Target,
    ExtraordinaryMechanicKind Kind);

public sealed record PreparedMutation(string Token, Action<ResolutionResult> Apply);

/// <summary>
/// Um token que uma mecânica aceita: <see cref="Head"/> literal (<c>npc.health</c>) ou prefixo
/// aberto terminado em <c>.</c> ou <c>:</c> (<c>area:radius:</c>); <see cref="Format"/> é o
/// formato legível para editores; <see cref="Target"/> diz quem sofre (<c>target</c>,
/// <c>carrier</c>, <c>area</c>, <c>world</c>, <c>household</c>).
/// </summary>
public sealed record ExtraordinaryPrimitive(string Head, string Format, bool SignedMagnitude, string Target)
{
    public bool Matches(string token)
    {
        if (!token.StartsWith(Head, StringComparison.Ordinal))
            return false;
        char last = Head[^1];
        return last is '.' or ':' || token.Length == Head.Length || token[Head.Length] == ':';
    }
}

public interface IExtraordinaryMechanic
{
    string Prefix { get; }
    ExtraordinaryMechanicKind Kind { get; }

    /// <summary>Tokens que esta mecânica aceita. Fonte única do catálogo e da validação na carga.</summary>
    IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; }

    Result<PreparedMutation?> PrepareEffect(ExtraordinaryMechanicContext ctx, string declaration);

    Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key);

    Result<PreparedMutation> PrepareCost(ExtraordinaryMechanicContext ctx, string declaration, int amount);
}

public interface IExtraordinaryMechanicRegistry
{
    IExtraordinaryMechanic? Resolve(string token);
}

public abstract class ExtraordinaryMechanic : IExtraordinaryMechanic
{
    public abstract string Prefix { get; }
    public abstract ExtraordinaryMechanicKind Kind { get; }
    public abstract IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; }

    public virtual Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

    public virtual Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
        => Result<long>.Fail($"Costs: alvo não suportado '{key}'");

    public virtual Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
        => Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{declaration}'");
}
