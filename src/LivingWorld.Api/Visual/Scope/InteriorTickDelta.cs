using LivingWorld.Api.Visual.Projection;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Visual.Scope;

/// <summary>Delta de um interior observado (godot-server-deltas): quem entrou, andou no mesmo andar, trocou de andar ou
/// saiu desde o tick anterior, no mesmo envelope dos deltas de mundo e cidade. Só é publicado quando algo mudou.</summary>
public sealed record InteriorTickDelta(
    long Tick,
    BuildingId BuildingId,
    IReadOnlyList<InteriorOccupant> Entered,
    IReadOnlyList<InteriorOccupant> Moved,
    IReadOnlyList<InteriorOccupant> ChangedFloor,
    IReadOnlyList<NpcId> Exited,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Lights = null)
{
    /// <summary>#258: <c>Lights</c> = as peças (índice na mobília) com a luz LIGADA agora, só quando o conjunto mudou desde o tick anterior; o cliente acende só o que o motor diz.</summary>
    public bool IsEmpty => Entered.Count == 0 && Moved.Count == 0 && ChangedFloor.Count == 0 && Exited.Count == 0 && Lights is null;
}
