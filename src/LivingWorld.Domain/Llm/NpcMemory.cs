using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Llm;

/// <summary>As cinco categorias de memória do NPC: operacional (estado
/// de curto prazo), episódica (o que viveu), semântica (o que sabe), social (o que pensa de
/// quem) e cultural (o que a comunidade do NPC crê).</summary>
public enum MemoryCategory
{
    Operational,
    Episodic,
    Semantic,
    Social,
    Cultural,
}

/// <summary>Registro de memória de um NPC — todo registro carrega
/// importância (0-100), tick de origem, participantes e local, para <c>Recall</c> pontuar
/// por importância + recência + relevância. Vive em <c>WorldState</c>:
/// acima do limiar do cenário (<see cref="LlmRules.CanonicalMemoryImportanceThreshold"/>) é
/// canônica, abaixo é volátil e compactável livremente (T10, futuro).</summary>
public sealed record NpcMemory(
    long Id,
    NpcId OwnerId,
    MemoryCategory Category,
    string Content,
    int Importance,
    long OriginTick,
    IReadOnlyList<NpcId> Participants,
    CellCoord Location);
