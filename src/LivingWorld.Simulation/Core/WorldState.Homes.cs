namespace LivingWorld.Simulation.Core;

// Parte de WorldState: a obra do morador na casa (#264). O estado mora em Building.Edit (nulo = nada mudou, fora do snapshot e do hash).
public sealed partial class WorldState
{
    /// <summary>Marca a lista de prédios como suja para o hash: a edição de um prédio (móvel, parede, pintura) mudou.</summary>
    internal void TouchBuildings() => CanonicalHashCache.MarkPropertyDirty(nameof(Buildings));
}
