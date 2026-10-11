using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre poderes, construtos e combate. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    internal void UpsertExtraordinaryCarrier(ExtraordinaryCarrierState carrier)
    {
        _extraordinaryCarriers.RemoveAll(existing => existing.CarrierId == carrier.CarrierId);
        _extraordinaryCarriers.Add(carrier);
        _extraordinaryCarriers.Sort((left, right) => left.CarrierId.Value.CompareTo(right.CarrierId.Value));
    }

    internal bool RemoveExtraordinaryCarrier(NpcId carrierId) =>
        _extraordinaryCarriers.RemoveAll(existing => existing.CarrierId == carrierId) > 0;

    internal long NextExtraordinaryConstructIdAndAdvance() => _nextExtraordinaryConstructId++;

    internal void AddExtraordinaryConstruct(ExtraordinaryConstruct construct)
    {
        _extraordinaryConstructs.Add(construct);
        _extraordinaryConstructs.Sort((left, right) => left.Id.CompareTo(right.Id));
    }

    internal bool RemoveExtraordinaryConstruct(long id) =>
        _extraordinaryConstructs.RemoveAll(construct => construct.Id == id) > 0;

    internal void ReplaceExtraordinaryConstruct(ExtraordinaryConstruct construct)
    {
        RemoveExtraordinaryConstruct(construct.Id);
        AddExtraordinaryConstruct(construct);
    }

    internal CombatEncounterId NextCombatEncounterIdAndAdvance() => new(_nextCombatEncounterId++);

    public void AddCombatEncounter(CombatEncounter encounter)
    {
        _combatEncounters.Add(encounter);
        _combatEncountersById[encounter.Id] = encounter;
        _combatEncounters.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
        CanonicalHashCache.MarkPropertyDirty(nameof(CombatEncounters));
        CanonicalHashCache.MarkPropertyDirty(nameof(NextCombatEncounterId));
    }

    public CombatEncounter? FindCombatEncounter(CombatEncounterId id) =>
        _combatEncountersById.GetValueOrDefault(id);

    internal void ReplaceCombatEncounter(CombatEncounter encounter)
    {
        _combatEncounters.RemoveAll(existing => existing.Id == encounter.Id);
        _combatEncountersById.Remove(encounter.Id);
        AddCombatEncounter(encounter);
    }

    public bool IsExtraordinaryConstructCell(CellCoord cell) =>
        _extraordinaryConstructs.Any(construct => construct.Footprint.Contains(cell));
}
