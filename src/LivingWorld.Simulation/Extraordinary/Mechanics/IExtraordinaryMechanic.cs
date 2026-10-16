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

public interface IExtraordinaryMechanic
{
    string Prefix { get; }
    ExtraordinaryMechanicKind Kind { get; }

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

    public virtual Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
        => Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

    public virtual Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
        => Result<long>.Fail($"Costs: alvo não suportado '{key}'");

    public virtual Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
        => Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{declaration}'");
}
