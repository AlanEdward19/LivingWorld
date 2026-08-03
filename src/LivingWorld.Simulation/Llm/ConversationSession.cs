using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Sessão de conversa jogador↔NPC (Fase 11, LLM-03) — nunca canônica: vive só em
/// memória do <see cref="ConversationSessionStore"/>, fora do snapshot/hash do mundo (mesma
/// lógica de "conversa não é fato do mundo" do design). <see cref="SessionId"/> é <c>long</c>
/// monotônico do próprio store — nunca <c>Guid.NewGuid()</c> (banido em Domain/Simulation,
/// rules/simulation-determinism.md), mesmo molde de <see cref="ScheduledEvent.Id"/>.</summary>
public sealed record ConversationSession(long SessionId, NpcId NpcId, long OpenedAtTick, long LastTurnTick, bool IsActive)
{
    internal ConversationSession WithTurn(long turnTick) => this with { LastTurnTick = turnTick };

    internal ConversationSession Ended() => this with { IsActive = false };
}
