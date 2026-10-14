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

/// <summary>Ciclo start/send/end de uma <see cref="ConversationSession"/>. A expiração é preguiçosa e
/// não toca o mundo: cada sessão guarda o tick em que vence (só neste store, fora do snapshot) e é encerrada
/// no primeiro <see cref="SendPlayerMessage"/> ou <see cref="Tick"/> depois dele. Antes, agendar o evento
/// consumia um id canônico do mundo — abrir uma conversa mudava o hash. Implementa <see
/// cref="ISimulationSystem"/> só para o <see cref="Tick"/>, que varre apenas as sessões ativas.</summary>
[LivingCapability("CONVERSATION", Consumers = ["interaction.conversation"])]
public sealed class ConversationSessionStore : ISimulationSystem
{
    public const string SystemName = "llm-conversation-expiry";

    private readonly Dictionary<long, ConversationSession> _sessions = new();
    private readonly Dictionary<long, List<(long TurnId, string PlayerText)>> _turnsBySession = new();
    private readonly Dictionary<long, long> _expiresAtTick = new();
    private long _nextSessionId;
    private long _nextTurnId;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (_expiresAtTick.Count == 0) return;
        foreach (var sessionId in _expiresAtTick.Where(e => e.Value <= ctx.CurrentTick).Select(e => e.Key).ToList())
            Expire(sessionId);
    }

    private void Expire(long sessionId)
    {
        _expiresAtTick.Remove(sessionId);
        if (_sessions.TryGetValue(sessionId, out var session) && session.IsActive)
            _sessions[sessionId] = session.Ended();
    }

    public ConversationSession? Find(long sessionId) => _sessions.GetValueOrDefault(sessionId);

    /// <summary>Turnos em ordem de <c>TurnId</c> monotônico — nunca ordem de chegada (Edge Case
    /// da spec: mensagens fora de ordem são serializadas por <c>TurnId</c>).</summary>
    public IReadOnlyList<(long TurnId, string PlayerText)> TurnsOf(long sessionId) =>
        _turnsBySession.TryGetValue(sessionId, out var turns) ? turns : [];

    /// <summary>Avalia disponibilidade social do NPC no tick atual (<see
    /// cref="ConversationAvailabilityPolicy"/>) e, só se aceito, abre a sessão e
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
        _expiresAtTick[sessionId] = ctx.CurrentTick + expireAfterTicks;

        return (ConversationStartDecision.Accepted, session);
    }

    /// <summary>AC2: registra o turno do jogador com <c>TurnId</c> monotônico. A ligação com o
    /// pipeline contexto→LLM→validação→resposta não é responsabilidade deste store (fora do
    /// escopo deste tipo — ver <c>ILlmProvider</c> e a validação de resposta).</summary>
    public SendMessageResult SendPlayerMessage(long sessionId, string text, TickContext ctx)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return SendMessageResult.SessionNotFound;
        if (_expiresAtTick.TryGetValue(sessionId, out var expiresAt) && expiresAt <= ctx.CurrentTick)
        {
            Expire(sessionId);
            return SendMessageResult.SessionInactive;
        }
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
        _expiresAtTick.Remove(sessionId);
        _sessions[sessionId] = session.Ended();
        return EndConversationResult.Ok;
    }
}
