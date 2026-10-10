using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Extraordinary;

/// <summary>Seleção volátil de poder no momento da decisão —
/// setada quando <see cref="ActionType.UsePower"/> vence utility; consumida na execução.
/// Nunca canônica / nunca serializada no snapshot do NPC.</summary>
public sealed record PendingPowerInvocation(
    string PowerId,
    string MechanicToken,
    NpcId? SuggestedTarget);
