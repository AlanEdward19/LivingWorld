using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre poderes, construtos e combate. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    private long _carriersVersion;
    private (long Carriers, AliveNpcIndex Index, long Alive, List<(ExtraordinaryCarrierState State, LivingWorld.Domain.Population.Npc Npc)> List)? _livingCarriers;

    /// <summary>Portadores vivos, na ordem de id da lista canônica, com o NPC já achado (AD-156, 08b item 106). Os mortos
    /// continuam em <see cref="ExtraordinaryCarriers"/> (snapshot e história), mas as varreduras horárias não passam por
    /// eles. Refeito só quando a lista de portadores ou o conjunto de vivos muda; quem usa ainda confere
    /// <c>IsAlive</c>.</summary>
    internal IReadOnlyList<(ExtraordinaryCarrierState State, LivingWorld.Domain.Population.Npc Npc)> LivingCarriers()
    {
        if (_livingCarriers is { } c && c.Carriers == _carriersVersion && ReferenceEquals(c.Index, AliveNpcIndex)
            && c.Alive == AliveNpcIndex.Version)
            return c.List;
        var list = new List<(ExtraordinaryCarrierState, LivingWorld.Domain.Population.Npc)>();
        foreach (var state in _extraordinaryCarriers)
            if (FindNpc(state.CarrierId) is { IsAlive: true } npc) list.Add((state, npc));
        _livingCarriers = (_carriersVersion, AliveNpcIndex, AliveNpcIndex.Version, list);
        return list;
    }

    internal void UpsertExtraordinaryCarrier(ExtraordinaryCarrierState carrier)
    {
        _carriersVersion++;
        // a lista fica ordenada por id: substitui no lugar ou insere na posição (antes, remover + ordenar tudo a cada
        // atualização horária de cada portador era quadrático no número de portadores; AD-156, com a herança genética
        // os portadores deixam de se extinguir)
        int low = 0, high = _extraordinaryCarriers.Count - 1;
        long id = carrier.CarrierId.Value;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            long value = _extraordinaryCarriers[mid].CarrierId.Value;
            if (value == id)
            {
                _extraordinaryCarriers[mid] = carrier;
                return;
            }
            if (value < id) low = mid + 1;
            else high = mid - 1;
        }
        _extraordinaryCarriers.Insert(low, carrier);
    }

    internal bool RemoveExtraordinaryCarrier(NpcId carrierId)
    {
        _carriersVersion++;
        return _extraordinaryCarriers.RemoveAll(existing => existing.CarrierId == carrierId) > 0;
    }

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

    public bool IsExtraordinaryConstructCell(CellCoord cell)
    {
        // Laço sem lambda: roda por NPC e por passo; o lambda capturava a célula e alocava a cada chamada.
        for (int i = 0; i < _extraordinaryConstructs.Count; i++)
            if (_extraordinaryConstructs[i].Footprint.Contains(cell)) return true;
        return false;
    }

    /// <summary>Estado de portador de poder do NPC, ou nulo. A lista é ordenada por id: busca binária, e sem portador
    /// nenhum (o caso comum) não aloca nada — antes cada consulta alocava lambda e delegate.</summary>
    public ExtraordinaryCarrierState? CarrierOf(NpcId id)
    {
        int low = 0, high = _extraordinaryCarriers.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            long value = _extraordinaryCarriers[mid].CarrierId.Value;
            if (value == id.Value) return _extraordinaryCarriers[mid];
            if (value < id.Value) low = mid + 1; else high = mid - 1;
        }

        return null;
    }
}
