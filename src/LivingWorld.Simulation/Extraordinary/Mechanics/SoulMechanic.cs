using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Persistência opt-in após a morte: <c>soul.persist-as-ghost</c> marca o portador
/// <see cref="Npc.IsGhost"/> quando ele morre. Sem o poder (ou com Extraordinary
/// desligado) a morte permanece terminal.
/// </summary>
public sealed class SoulMechanic : ExtraordinaryMechanic
{
    public const string PersistAsGhost = "soul.persist-as-ghost";

    public override string Prefix => "soul.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public static readonly ExtraordinaryPrimitive<bool> PersistAsGhostToken = new(
        PersistAsGhost, "soul.persist-as-ghost", false, "carrier", PrimitiveSyntax.Flag(PersistAsGhost));

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [PersistAsGhostToken];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = PersistAsGhostToken.Parse(declaration);
        return parsed.IsSuccess
            ? Result<PreparedMutation?>.Ok(null)
            : Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
    }

    public static void OnCarrierDeath(WorldState world, Npc npc)
    {
        if (!world.Extraordinary.Enabled) return;
        if (!CarriesPersistAsGhost(world, npc)) return;
        npc.BecomeGhost();
    }

    public static GhostQuery? TryQuery(WorldState world, NpcId id)
    {
        var npc = world.FindNpc(id);
        if (npc is not { IsGhost: true }) return null;
        return new GhostQuery(npc.Id, npc.Name, npc.CurrentLocation, npc.Personality, npc.Skills);
    }

    private static bool CarriesPersistAsGhost(WorldState world, Npc npc)
    {
        var carrier = world.CarrierOf(npc.Id);
        if (carrier is null) return false;
        return world.Extraordinary.Descriptors.Any(descriptor =>
            carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal)
            && descriptor.Effects.Contains(PersistAsGhost, StringComparer.Ordinal));
    }

    public readonly record struct GhostQuery(
        NpcId Id, string Name, CellCoord LastPosition, Personality Personality, SkillSet Skills);
}
