using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Jogador é um NPC com controlador externo (Fase 25): encarnar só grava <see cref="Npc.ControllerId"/>; a ação pedida
/// substitui a escolha autônoma na <c>BehaviorDecisionSystem</c> e passa pela mesma resolução; sair é desaparecimento
/// (<see cref="Disappearance"/>, o mesmo caminho do NPC do motor que some), não congelamento. Encarnar quem estava sumido é a volta
/// tardia (<see cref="LateReturn"/>). Erros começam com <c>not_found:</c> ou <c>conflict:</c> para a borda mapear 404 e 409.</summary>
public static class PlayerCommands
{
    public const string NotFound = "not_found:";
    public const string Conflict = "conflict:";

    /// <summary>Ações do catálogo que o jogador escolhe. <c>Travel</c> nasce da resolução de outra ação; <c>UsePower</c> (AD-196)
    /// exige um poder que o NPC porta, e a invocação sai da mesma lista de oportunidades do motor.</summary>
    public static readonly IReadOnlySet<ActionType> Choosable =
        new HashSet<ActionType>
        {
            ActionType.Eat, ActionType.Sleep, ActionType.Work, ActionType.Socialize, ActionType.Idle, ActionType.Buy, ActionType.UsePower,
        };

    /// <summary>Vínculo (0–100) a partir do qual alguém dá pela falta do desaparecido.</summary>
    public const double CloseBond = Disappearance.CloseBond;

    public static Result<Unit> Incarnate(WorldState world, TickContext ctx, NpcId npcId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return Result<Unit>.Fail("playerId: obrigatório");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail($"{NotFound} NPC ausente ou morto");
        if (npc.ControllerId is not null) return Result<Unit>.Fail($"{Conflict} NPC já tem controlador");
        if (ControlledBy(world, playerId) is not null) return Result<Unit>.Fail($"{Conflict} jogador já controla outro NPC");

        if (npc.DisappearedAtTick is not null) LateReturn.Apply(world, ctx, npc, LateReturn.ByPlayer); // voltar depois de sumido
        var result = npc.Incarnate(playerId);
        if (!result.IsSuccess) return result;
        // AD-196: encarnar não é fato do mundo (ninguém dentro dele percebe quem conduz): fica no registro de sessões do hospedeiro
        if (!world.Players.Enabled)
            ctx.LogEvent(WorldEventKind.PlayerIncarnated, $"{npc.Id.Value}|{playerId}", sourceSystem: "PlayerCommands");
        return result;
    }

    /// <summary><paramref name="action"/> nulo devolve a escolha ao motor. <paramref name="powerId"/> só vale com
    /// <see cref="ActionType.UsePower"/> e tem de ser um poder que o NPC porta.</summary>
    public static Result<Unit> Act(WorldState world, NpcId npcId, string playerId, ActionType? action, string? powerId = null)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (action is { } chosen && !Choosable.Contains(chosen)) return Result<Unit>.Fail("action: fora do que o jogador pode escolher");
        if (action == ActionType.UsePower && !world.Players.Enabled) return Result<Unit>.Fail("action: fora do que o jogador pode escolher");
        if (action == ActionType.UsePower && !Carries(world, npc, powerId))
            return Result<Unit>.Fail("powerId: o NPC não porta esse poder");
        npc.SetControlledAction(action, powerId);
        return Result<Unit>.Ok(Unit.Value);
    }

    private static bool Carries(WorldState world, Npc npc, string? powerId) =>
        !string.IsNullOrWhiteSpace(powerId)
        && world.ExtraordinaryCarriers.Any(c => c.CarrierId == npc.Id && c.PowerIds.Contains(powerId));

    public static Npc? ControlledBy(WorldState world, string playerId) =>
        world.Npcs.FirstOrDefault(n => n.IsAlive && n.ControllerId == playerId);

    /// <summary>Logout: o NPC some pelo mesmo caminho do NPC do motor que some (<see cref="Disappearance"/>).</summary>
    public static Result<Npc> Logout(WorldState world, TickContext ctx, string playerId)
    {
        var npc = ControlledBy(world, playerId);
        if (npc is null) return Result<Npc>.Fail($"{NotFound} jogador não controla nenhum NPC");
        Disappearance.Apply(world, ctx, npc, playerId);
        return Result<Npc>.Ok(npc);
    }
}
