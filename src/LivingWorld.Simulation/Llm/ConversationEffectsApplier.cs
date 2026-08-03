using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Aplica só os efeitos permitidos de um turno já validado (Fase 11, LLM-09 AC3):
/// memória episódica da conversa e relação NPC↔jogador. Mesmo espírito de <see
/// cref="ConversationSession"/>/<see cref="ConversationSessionStore"/>: vive só em memória, nunca
/// no snapshot/hash canônico do mundo — não existe ainda um "jogador" endereçável em <c>Npc</c>
/// (<see cref="Relationship"/> é sempre NPC↔NPC), então a relação NPC↔jogador desta fase é um
/// contador dedicado por <see cref="NpcId"/>, não uma gravação em <c>WorldState</c>.</summary>
public sealed class ConversationEffectsApplier
{
    private const double TrustDeltaPerValidatedTurn = 1.0;
    private const double MaxPlayerTrust = 100.0;

    private readonly Dictionary<long, List<string>> _episodicMemoryBySession = new();
    private readonly Dictionary<NpcId, double> _playerTrustByNpc = new();

    public IReadOnlyList<string> EpisodicMemoryOf(long sessionId) =>
        _episodicMemoryBySession.TryGetValue(sessionId, out var memory) ? memory : [];

    public double PlayerTrustOf(NpcId npcId) => _playerTrustByNpc.GetValueOrDefault(npcId);

    public void Apply(NpcId npcId, long sessionId, ValidatedLlmTurn turn)
    {
        if (!_episodicMemoryBySession.TryGetValue(sessionId, out var memory))
            _episodicMemoryBySession[sessionId] = memory = [];
        memory.Add(turn.Dialogue);

        _playerTrustByNpc[npcId] = Math.Min(MaxPlayerTrust, PlayerTrustOf(npcId) + TrustDeltaPerValidatedTurn);
    }
}
