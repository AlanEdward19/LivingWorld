using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Llm;

/// <summary>Aplica só os efeitos permitidos de um turno já validado (Fase 11, LLM-09 AC3):
/// memória episódica canônica do NPC (<see cref="WorldState.AddNpcMemory"/>, T9b) e a relação
/// NPC->jogador real (<see cref="Relationship"/>/<see cref="RelationshipKey"/>, Fase 7) — não mais
/// uma lista efêmera por sessão nem um contador de trust dedicado (gap documentado do T6
/// original). O jogador não é um <see cref="Npc"/> de verdade, então não tem <see cref="NpcId"/>
/// próprio: <see cref="PlayerNpcId"/> é um sentinela reservado (<c>NpcId(-1)</c>) que nunca colide
/// com um NPC real, já que <see cref="WorldState.NextNpcIdAndAdvance"/> só produz ids >= 0.</summary>
public sealed class ConversationEffectsApplier
{
    /// <summary>Id sentinela do jogador em <see cref="Relationship"/>/<see cref="NpcMemory"/>.</summary>
    public static readonly NpcId PlayerNpcId = new(-1);

    /// <summary>Importância fixa e conservadora (0-100) da memória episódica de um turno de
    /// conversa — abaixo do limiar canônico default (<see
    /// cref="LlmRules.CanonicalMemoryImportanceThreshold"/> = 50), então uma conversa cotidiana
    /// nasce volátil (compactável) por padrão. A spec desta task não define uma fórmula de
    /// significância do evento, então o valor é fixo em vez de inventada uma (decisão
    /// documentada, não uma lacuna).</summary>
    private const int ConversationMemoryImportance = 30;

    private readonly LlmRules _llmRules;

    public ConversationEffectsApplier(LlmRules? llmRules = null) => _llmRules = llmRules ?? LlmRules.Default;

    public void Apply(WorldState world, Npc npc, long tick, ValidatedLlmTurn turn)
    {
        world.AddNpcMemory(
            ownerId: npc.Id,
            category: MemoryCategory.Episodic,
            content: turn.Dialogue,
            importance: ConversationMemoryImportance,
            originTick: tick,
            participants: [PlayerNpcId],
            location: npc.CurrentLocation,
            canonicalImportanceThreshold: _llmRules.CanonicalMemoryImportanceThreshold);

        var relationship = world.GetOrCreateRelationship(new RelationshipKey(npc.Id, PlayerNpcId), tick);
        relationship.ApplyEvent(RelationshipEventType.Conversation, world.FamilyRules);
        relationship.MarkContact(tick);
    }
}
