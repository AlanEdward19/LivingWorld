using LivingWorld.Domain;

namespace LivingWorld.Simulation;

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

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (declaration != PersistAsGhost)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        return Result<PreparedMutation?>.Ok(null);
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
        var carrier = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (carrier is null) return false;
        return world.Extraordinary.Descriptors.Any(descriptor =>
            carrier.PowerIds.Contains(descriptor.Id, StringComparer.Ordinal)
            && descriptor.Effects.Contains(PersistAsGhost, StringComparer.Ordinal));
    }

    public readonly record struct GhostQuery(
        NpcId Id, string Name, CellCoord LastPosition, Personality Personality, SkillSet Skills);
}
