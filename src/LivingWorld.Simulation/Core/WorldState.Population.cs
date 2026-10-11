using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Scheduling;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre NPCs, domicílios, relações, despertares e camas. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    /// <summary>Único ponto de criação de uma <see cref="Relationship"/> — cria só na
    /// primeira chamada para a chave; chamadas seguintes devolvem a mesma instância.</summary>
    internal Relationship GetOrCreateRelationship(RelationshipKey key, long now)
    {
        if (!_relationships.TryGetValue(key, out var relationship))
        {
            relationship = Relationship.Initial(now);
            _relationships[key] = relationship;
            IndexRelationshipTarget(key);
        }
        return relationship;
    }

    /// <summary>Alvos das relações já existentes de <paramref name="from"/>, em ordem crescente de Id.</summary>
    internal IReadOnlyList<NpcId> RelationshipTargetsOf(NpcId from) =>
        _relationshipTargetsByFrom.TryGetValue(from, out var targets) ? targets : [];

    private void IndexRelationshipTarget(RelationshipKey key)
    {
        if (!_relationshipTargetsByFrom.TryGetValue(key.From, out var targets))
            _relationshipTargetsByFrom[key.From] = targets = [];
        int lo = 0, hi = targets.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (targets[mid].Value < key.To.Value) lo = mid + 1; else hi = mid;
        }
        targets.Insert(lo, key.To);
    }

    private void UnindexRelationshipTarget(RelationshipKey key)
    {
        if (!_relationshipTargetsByFrom.TryGetValue(key.From, out var targets)) return;
        targets.Remove(key.To);
        if (targets.Count == 0) _relationshipTargetsByFrom.Remove(key.From);
    }

    internal void ClearNpcWakeBatch() => _npcWakeBatch.Clear();

    internal void AddNpcWake(Npc npc) => _npcWakeBatch.Add(npc);

    internal void ReplaceNpcWake(TickContext ctx, long npcId, long targetTick)
    {
        if (_npcWakeEventIdByNpc.TryGetValue(npcId, out var oldId))
            _scheduler.Cancel(oldId);

        var evt = ctx.ScheduleEvent(targetTick, NpcWakeScheduler.SystemName, npcId.ToString());
        _npcWakeEventIdByNpc[npcId] = evt.Id;
    }

    internal void ClearNpcWakeEvent(long npcId) => _npcWakeEventIdByNpc.Remove(npcId);

    internal NpcId NextNpcIdAndAdvance() => new(_nextNpcId++);
    internal HouseholdId NextHouseholdIdAndAdvance() => new(_nextHouseholdId++);

    /// <summary>Reserva <paramref name="count"/> ids em lote (T50) — usado só na carga de
    /// cenário, pra dar a cada membro do <see cref="AggregatePopulationPool"/> autorado um
    /// <see cref="NpcId"/> estável antes de qualquer materialização. Mesmo contador de
    /// <see cref="NextNpcIdAndAdvance"/>, só avançado de uma vez em vez de um por vez.</summary>
    internal IReadOnlyList<NpcId> ReserveNpcIdBlock(long count)
    {
        var ids = new List<NpcId>();
        for (long i = 0; i < count; i++)
            ids.Add(NextNpcIdAndAdvance());
        return ids;
    }

    /// <summary>Sincroniza o contador depois de um lote gerado fora do tick (seed inicial),
    /// que consome ids diretamente do <see cref="PopulationGenerator"/> em vez de um por vez.</summary>
    internal void AdvanceNpcIdTo(long value)
    {
        _nextNpcId = Math.Max(_nextNpcId, value);
        MarkCanonicalPropertyDirty(nameof(NextNpcId));
    }

    internal void AdvanceHouseholdIdTo(long value)
    {
        _nextHouseholdId = Math.Max(_nextHouseholdId, value);
        MarkCanonicalPropertyDirty(nameof(NextHouseholdId));
    }

    internal void AddNpc(Npc npc)
    {
        npc.CanonicalMutationNotifier = MarkNpcCanonicalDirty;
        _npcs.Add(npc);
        _npcById[npc.Id] = npc;
        AliveNpcIndex.OnBorn(npc);
        CanonicalHashCache.MarkNpcsStructureDirty();
    }

    internal void AddHousehold(Household household)
    {
        _households.Add(household);
        _householdById[household.Id] = household;
        MarkCanonicalPropertyDirty(nameof(Households));
    }

    /// <summary>Household sem membros é dissolvido — sai da lista canônica.</summary>
    internal void RemoveHousehold(HouseholdId id)
    {
        _households.RemoveAll(h => h.Id == id);
        _householdById.Remove(id);
        MarkCanonicalPropertyDirty(nameof(Households));
    }

    // SPEC_DEVIATION: design.md não previa remover uma linha de Npc — mas
    // Dematerialize (approach A) exige exatamente isso ("remover a linha do store"). Mirror de
    // RemoveHousehold; só MaterializationSystem chama.
    internal void RemoveNpc(NpcId id)
    {
        if (_npcById.TryGetValue(id, out var npc) && npc.IsAlive)
            AliveNpcIndex.OnDied(npc);
        _npcs.RemoveAll(n => n.Id == id);
        _npcById.Remove(id);
        CanonicalHashCache.MarkNpcsStructureDirty();
    }

    private void BindNpcCanonicalNotifiers()
    {
        foreach (var npc in _npcs)
            npc.CanonicalMutationNotifier = MarkNpcCanonicalDirty;
    }

    private void MarkNpcCanonicalDirty(NpcId id) => CanonicalHashCache.MarkNpcDirty(id.Value);

    internal void MarkCanonicalPropertyDirty(string propertyName) =>
        CanonicalHashCache.MarkPropertyDirty(propertyName);

    internal Npc? FindNpc(NpcId id) => _npcById.GetValueOrDefault(id);
    internal Household? FindHousehold(HouseholdId id) => _householdById.GetValueOrDefault(id);

    internal int RemoveRelationshipsBetween(NpcId first, NpcId second)
    {
        int removed = 0;
        var forward = new RelationshipKey(first, second);
        var backward = new RelationshipKey(second, first);
        if (_relationships.Remove(forward)) { removed++; UnindexRelationshipTarget(forward); }
        if (_relationships.Remove(backward)) { removed++; UnindexRelationshipTarget(backward); }
        return removed;
    }

    internal RestPlaceId NextRestPlaceIdAndAdvance() => new(_nextRestPlaceId++);

    /// <summary>Cama de menor id do domicílio (a mesma que <c>Where(Bed &amp;&amp; dono).OrderBy(Id).First()</c>
    /// devolveria), sem varrer todas as camas. Índice derivado: <see cref="RestPlaces"/> só cresce por
    /// <see cref="AddRestPlace"/>, então basta indexar o que entrou desde a última consulta.</summary>
    internal RestPlace? BedOf(HouseholdId household)
    {
        for (; _bedsIndexedCount < _restPlaces.Count; _bedsIndexedCount++)
        {
            var place = _restPlaces[_bedsIndexedCount];
            if (place.Kind != RestPlaceKind.Bed) continue;
            if (!_bedByHousehold.TryGetValue(place.OwnerHousehold, out var known) || place.Id.Value < known.Id.Value)
                _bedByHousehold[place.OwnerHousehold] = place;
        }
        return _bedByHousehold.GetValueOrDefault(household);
    }

    public void AddRestPlace(RestPlace place)
    {
        _restPlaces.Add(place);
        _nextRestPlaceId = Math.Max(_nextRestPlaceId, place.Id.Value + 1);
    }
}
