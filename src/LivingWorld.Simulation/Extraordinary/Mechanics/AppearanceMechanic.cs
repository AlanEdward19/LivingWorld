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

    /// <summary>Parser único de <c>appearance.impersonate:&lt;npcId&gt;</c>.</summary>
    public static readonly ExtraordinaryPrimitive<long> Impersonate = new(
        ImpersonatePrefix, "appearance.impersonate:<npcId>", false, "carrier",
        static token => long.TryParse(token[ImpersonatePrefix.Length..], out long id) && id > 0
            ? Result<long>.Ok(id)
            : Result<long>.Fail($"Effects: alvo não suportado '{token}'"));

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Impersonate];

    public override Result<PreparedMutation?> PrepareEffect(
        ExtraordinaryMechanicContext ctx, string declaration)
    {
        var parsed = Impersonate.Parse(declaration);
        if (!parsed.IsSuccess)
            return Result<PreparedMutation?>.Fail($"Effects: alvo não suportado '{declaration}'");

        var impersonatedId = new NpcId(parsed.Value);
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
        var state = world.CarrierOf(npc.Id);
        if (state is null) return;
        world.UpsertExtraordinaryCarrier(state with { ImpersonatingId = null });
        ctx.LogEvent(WorldEventKind.IdentityChanged, $"{npc.Id.Value}|{impersonated.Value}|impersonate-revert", sourceSystem: "AppearanceMechanic");
    }
}
