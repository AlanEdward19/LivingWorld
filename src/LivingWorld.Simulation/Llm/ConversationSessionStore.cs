using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Llm;

public enum SendMessageResult { Ok, SessionNotFound, SessionInactive }

public enum EndConversationResult { Ok, SessionNotFound }

/// <summary>Ciclo start/send/end de uma <see cref="ConversationSession"/> (Fase 11, LLM-03,
/// story "Sessão de conversa segura" ACs 1-3). Expiração é agendada via <see
/// cref="TickContext.ScheduleEvent"/> — nunca varredura por tick (design.md, reuso de <see
/// cref="EventScheduler"/>). Implementa <see cref="ISimulationSystem"/> só para receber o
/// evento de expiração agendado por si mesmo; não faz trabalho nenhum por tick.</summary>
public sealed class ConversationSessionStore : ISimulationSystem
{
    public const string SystemName = "llm-conversation-expiry";

    private readonly Dictionary<long, ConversationSession> _sessions = new();
    private readonly Dictionary<long, List<(long TurnId, string PlayerText)>> _turnsBySession = new();
    private long _nextSessionId;
    private long _nextTurnId;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }

    public ConversationSession? Find(long sessionId) => _sessions.GetValueOrDefault(sessionId);

    /// <summary>Turnos em ordem de <c>TurnId</c> monotônico — nunca ordem de chegada (Edge Case
    /// da spec: mensagens fora de ordem são serializadas por <c>TurnId</c>).</summary>
    public IReadOnlyList<(long TurnId, string PlayerText)> TurnsOf(long sessionId) =>
        _turnsBySession.TryGetValue(sessionId, out var turns) ? turns : [];

    /// <summary>AC1: avalia disponibilidade social do NPC no tick atual (<see
    /// cref="ConversationAvailabilityPolicy"/>, LLM-01/02) e, só se aceito, abre a sessão e
    /// agenda a expiração <paramref name="expireAfterTicks"/> à frente.</summary>
    public (ConversationStartDecision Decision, ConversationSession? Session) StartConversation(
        Npc npc, NeedsRules needsRules, LlmRules llmRules, Relationship? relationshipToInitiator,
        TickContext ctx, long expireAfterTicks)
    {
        var decision = ConversationAvailabilityPolicy.Evaluate(npc, needsRules, llmRules, relationshipToInitiator, ctx.CurrentTick);
        if (decision.Result != ConversationStartDecision.Accepted)
            return (decision.Result, null);

        long sessionId = _nextSessionId++;
        var session = new ConversationSession(sessionId, npc.Id, ctx.CurrentTick, ctx.CurrentTick, IsActive: true);
        _sessions[sessionId] = session;
        _turnsBySession[sessionId] = [];
        ctx.ScheduleEvent(ctx.CurrentTick + expireAfterTicks, SystemName, sessionId.ToString());

        return (ConversationStartDecision.Accepted, session);
    }

    /// <summary>AC2: registra o turno do jogador com <c>TurnId</c> monotônico. A ligação com o
    /// pipeline contexto→LLM→validação→resposta não é responsabilidade deste store (fora do
    /// escopo desta task — ver <c>ILlmProvider</c>/T3 e as tasks de validação futuras).</summary>
    public SendMessageResult SendPlayerMessage(long sessionId, string text, TickContext ctx)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return SendMessageResult.SessionNotFound;
        if (!session.IsActive)
            return SendMessageResult.SessionInactive;

        _turnsBySession[sessionId].Add((_nextTurnId++, text));
        _sessions[sessionId] = session.WithTurn(ctx.CurrentTick);
        return SendMessageResult.Ok;
    }

    /// <summary>AC3: encerra a sessão sem apagar o histórico de turnos já registrado.</summary>
    public EndConversationResult EndConversation(long sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)) return EndConversationResult.SessionNotFound;
        _sessions[sessionId] = session.Ended();
        return EndConversationResult.Ok;
    }

    /// <summary>Dispara na expiração agendada (nunca varredura por tick) — mesmo efeito de <see
    /// cref="EndConversation"/>, histórico preservado.</summary>
    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (!long.TryParse(evt.Payload, out var sessionId)) return;
        if (_sessions.TryGetValue(sessionId, out var session) && session.IsActive)
            _sessions[sessionId] = session.Ended();
    }
}
