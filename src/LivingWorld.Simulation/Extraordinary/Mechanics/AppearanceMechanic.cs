using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

/// <summary>
/// Metamorfismo cosmético: o portador é observável como o alvo sem alterar <see cref="Npc.Id"/>.
/// </summary>
public sealed class AppearanceMechanic : ExtraordinaryMechanic
{
    public const string ImpersonatePrefix = "appearance.impersonate:";

    public override string Prefix => "appearance.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Effect;

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        if (!declaration.StartsWith(ImpersonatePrefix, StringComparison.Ordinal))
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");
        if (!long.TryParse(declaration[ImpersonatePrefix.Length..], out long targetValue)
            || targetValue <= 0)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var impersonatedId = new NpcId(targetValue);
        if (ctx.World.FindNpc(impersonatedId) is null)
            return Result<PreparedMutation?>.Fail("Effects: appearance.impersonate exige NPC existente");

        var world = ctx.World;
        var carrier = ctx.Carrier;
        var tick = ctx.Tick;
        return Result<PreparedMutation?>.Ok(new PreparedMutation(declaration, _ =>
        {
            var state = ControlMechanic.CarrierOf(world, carrier);
            if (state.ImpersonatingId == impersonatedId) return;
            world.UpsertExtraordinaryCarrier(state with { ImpersonatingId = impersonatedId });
            tick.LogEvent(WorldEventKind.IdentityChanged, $"{carrier.Id.Value}|{impersonatedId.Value}|impersonate", sourceSystem: "AppearanceMechanic");
        }));
    }

    internal static void RevertIfCeased(
        WorldState world, TickContext ctx, Npc npc, ExtraordinaryCarrierState previous)
    {
        if (previous.ImpersonatingId is not { } impersonated) return;
        if (previous.BodySwapPartner is not null) return;
        var state = world.ExtraordinaryCarriers.FirstOrDefault(item => item.CarrierId == npc.Id);
        if (state is null) return;
        world.UpsertExtraordinaryCarrier(state with { ImpersonatingId = null });
        ctx.LogEvent(WorldEventKind.IdentityChanged, $"{npc.Id.Value}|{impersonated.Value}|impersonate-revert", sourceSystem: "AppearanceMechanic");
    }
}
