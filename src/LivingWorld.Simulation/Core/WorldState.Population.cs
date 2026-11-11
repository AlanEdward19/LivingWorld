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
            _relationshipOrder.Append(key, relationship);
            IndexRelationshipTarget(key, relationship);
            AttachRelationshipStamp(key, relationship);
        }
        return relationship;
    }

    /// <summary>Alvos das relações já existentes de <paramref name="from"/>, em ordem crescente de Id.</summary>
    internal IReadOnlyList<NpcId> RelationshipTargetsOf(NpcId from) =>
        _relationshipTargetsByFrom.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>As relações de <paramref name="from"/> na mesma ordem de <see cref="RelationshipTargetsOf"/>, sem busca por
    /// chave: quem só precisa varrer os eixos do dono (<c>"tem vínculo forte?"</c>) não paga um hash por relação.</summary>
    internal List<Relationship>? RelationshipsFrom(NpcId from) => _relationshipsByFrom.GetValueOrDefault(from);

    private void IndexRelationshipTarget(RelationshipKey key, Relationship relationship)
    {
        if (!_relationshipTargetsByFrom.TryGetValue(key.From, out var targets))
        {
            _relationshipTargetsByFrom[key.From] = targets = [];
            _relationshipsByFrom[key.From] = [];
        }
        int lo = 0, hi = targets.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (targets[mid].Value < key.To.Value) lo = mid + 1; else hi = mid;
        }
        targets.Insert(lo, key.To);
        _relationshipsByFrom[key.From].Insert(lo, relationship);
    }

    /// <summary>Liga a relação ao carimbo do dono e o avança (o conjunto de relações dele mudou).</summary>
    private void AttachRelationshipStamp(RelationshipKey key, Relationship relationship)
    {
        if (!_relationshipStampByFrom.TryGetValue(key.From, out var stamp))
            _relationshipStampByFrom[key.From] = stamp = new RelationshipAxesStamp();
        relationship.OwnerStamp = stamp;
        stamp.Touch();
    }

    /// <summary>Versão das relações de <paramref name="from"/>: igual à da última consulta = nenhuma relação dele foi
    /// criada, removida nem teve eixo alterado.</summary>
    internal long RelationshipAxesVersionOf(NpcId from) =>
        _relationshipStampByFrom.TryGetValue(from, out var stamp) ? stamp.Version : 0;

    private void UnindexRelationshipTarget(RelationshipKey key)
    {
        if (_relationshipStampByFrom.TryGetValue(key.From, out var stamp)) stamp.Touch();
        if (!_relationshipTargetsByFrom.TryGetValue(key.From, out var targets)) return;
        int index = targets.IndexOf(key.To);
        if (index < 0) return;
        targets.RemoveAt(index);
        _relationshipsByFrom[key.From].RemoveAt(index);
        if (targets.Count == 0)
        {
            _relationshipTargetsByFrom.Remove(key.From);
            _relationshipsByFrom.Remove(key.From);
        }
    }

    internal void AddNpcWake(Npc npc) => _npcWakeBatch.Add(npc);

    /// <summary>Agenda (ou move) o despertar do NPC. A sequência sai do contador de <see cref="NextEventId"/>, como
    /// quando o despertar era um <see cref="ScheduledEvent"/>: os ids dos outros eventos e a ordem do lote ficam iguais.</summary>
    internal void ReplaceNpcWake(long npcId, long targetTick) =>
        _npcWakes.Schedule(npcId, targetTick, NextEventIdAndAdvance());

    /// <summary>Monta <see cref="NpcWakeBatch"/> com os despertares que vencem em <paramref name="tick"/>: vivos, por
    /// sequência decrescente. O despertar de quem morreu ou saiu do mundo é consumido sem acordar ninguém.</summary>
    internal void PrepareNpcWakeBatch(long tick)
    {
        _npcWakeBatch.Clear();
        _npcWakes.TakeDue(tick, _dueNpcWakeIds);
        foreach (var npcId in _dueNpcWakeIds)
            if (FindNpc(new NpcId(npcId)) is { IsAlive: true } npc)
                _npcWakeBatch.Add(npc);
    }

    /// <summary>Quem acordaria em <paramref name="tick"/>, na ordem do lote, sem consumir — para testes e inspeção.</summary>
    internal IReadOnlyList<long> PeekDueNpcWakes(long tick) => _npcWakes.PeekDue(tick);

    /// <summary>Tick do despertar pendente do NPC, ou nulo se não houver.</summary>
    internal long? PendingWakeTickOf(long npcId) => _npcWakes.TickOf(npcId);

    internal void RestoreNpcWakes(IEnumerable<PendingNpcWake> wakes) => _npcWakes.Restore(wakes);

    internal NpcId NextNpcIdAndAdvance() => new(_nextNpcId++);
    internal HouseholdId NextHouseholdIdAndAdvance() => new(_nextHouseholdId++);

    /// <summary>Reserva <paramref name="count"/> ids em lote — usado só na carga de
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

    /// <summary>Remove toda relação de e para <paramref name="id"/>: usado quando o NPC deixa de existir no mundo (arquivo
    /// frio). Sem isso o par ficava para sempre apontando para quem já não está em <see cref="Npcs"/>, e a seção crescia
    /// com a idade do mundo, não com a população (#155, #157).</summary>
    internal int RemoveRelationshipsOf(NpcId id)
    {
        int removed = 0;
        if (_relationshipTargetsByFrom.TryGetValue(id, out var targets))
            foreach (var to in targets.ToArray())
            {
                var key = new RelationshipKey(id, to);
                if (RemoveRelationship(key)) removed++;
            }

        // Quem tinha relação com ele: a ordem não importa (cada remoção só mexe no próprio par), mas vai ordenada por Id.
        var owners = _relationshipTargetsByFrom.Keys.ToArray();
        Array.Sort(owners, (a, b) => a.Value.CompareTo(b.Value));
        foreach (var from in owners)
        {
            var key = new RelationshipKey(from, id);
            if (RemoveRelationship(key)) removed++;
        }
        return removed;
    }

    /// <summary>Remove um par orientado (o vínculo esquecido de A por B não apaga o de B por A).</summary>
    internal bool RemoveRelationship(RelationshipKey key)
    {
        if (!_relationships.Remove(key, out var removed)) return false;
        _relationshipOrder.Remove(removed);
        UnindexRelationshipTarget(key);
        return true;
    }

    internal int RemoveRelationshipsBetween(NpcId first, NpcId second)
    {
        int removed = 0;
        var forward = new RelationshipKey(first, second);
        var backward = new RelationshipKey(second, first);
        if (RemoveRelationship(forward)) removed++;
        if (RemoveRelationship(backward)) removed++;
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
