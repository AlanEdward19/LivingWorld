using LivingWorld.Domain.Health;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState: infecções e imunidades (disease-epidemics, AD-137). As duas listas ficam ordenadas por NPC e doença:
// toda consulta é busca binária, e quem não está doente custa O(log doentes).
public sealed partial class WorldState
{
    // Derivado (fora do hash): quem está cuidando de um doente, ordenado por id, refeito quando as infecções mudam.
    private long[] _caregivers = [];
    private int _caregiversVersion = -1;
    private int _infectionsVersion;

    public bool IsInfected(NpcId npc, int disease) => IndexOfInfection(npc, disease) >= 0;

    public bool IsImmune(NpcId npc, int disease)
    {
        int index = IndexOfImmunity(npc, disease);
        return index >= 0 && _immunities[index].UntilTick > CurrentDate.TotalHours;
    }

    /// <summary>Infecção do modelo antigo: já doente, sem incubação nem desfecho decidido.</summary>
    public void AddInfection(NpcId npc, int disease, long endTick) =>
        AddInfection(new Infection(npc, disease, endTick)
        {
            InfectedTick = CurrentDate.TotalHours,
            OnsetTick = CurrentDate.TotalHours,
            Symptomatic = true,
        });

    public void AddInfection(Infection infection)
    {
        int at = IndexOfInfection(infection.Npc, infection.Disease);
        if (at >= 0) return;
        _infections.Insert(~at, infection);
        TouchInfections();
    }

    /// <summary>Troca a infecção do mesmo NPC e doença (mudança de fase).</summary>
    public void ReplaceInfection(Infection infection)
    {
        int at = IndexOfInfection(infection.Npc, infection.Disease);
        if (at < 0) return;
        _infections[at] = infection;
        TouchInfections();
    }

    public void RemoveInfection(NpcId npc, int disease)
    {
        int index = IndexOfInfection(npc, disease);
        if (index < 0) return;
        _infections.RemoveAt(index);
        TouchInfections();
    }

    /// <summary>Primeira posição em <see cref="Infections"/> do NPC (as dele vêm em seguida, por doença); −1 se não tem.</summary>
    public int FirstInfectionIndex(NpcId npc)
    {
        int index = LowerBound(_infections, npc.Value, int.MinValue, static i => (i.Npc.Value, i.Disease));
        return index < _infections.Count && _infections[index].Npc == npc ? index : -1;
    }

    /// <summary>Primeira posição com chave ≥ (npc, doença) numa lista ordenada, sem alocar (consulta por NPC por dia).</summary>
    private static int LowerBound<T>(List<T> list, long npc, int disease, Func<T, (long Npc, int Disease)> key)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            var (n, d) = key(list[mid]);
            if (n < npc || (n == npc && d < disease)) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>Quem morreu sai dos dois livros (a infecção em curso e a imunidade não servem mais a ninguém).</summary>
    public void ForgetHealthOf(NpcId npc)
    {
        for (int i = FirstInfectionIndex(npc); i >= 0 && i < _infections.Count && _infections[i].Npc == npc;)
        {
            _infections.RemoveAt(i);
            TouchInfections();
        }

        int first = LowerBound(_immunities, npc.Value, int.MinValue, static i => (i.Npc.Value, i.Disease));
        int count = 0;
        while (first + count < _immunities.Count && _immunities[first + count].Npc == npc) count++;
        if (count == 0) return;
        _immunities.RemoveRange(first, count);
        CanonicalHashCache.MarkPropertyDirty(nameof(Immunities));
    }

    public void SetImmunity(NpcId npc, int disease, long untilTick)
    {
        int index = IndexOfImmunity(npc, disease);
        var immunity = new Immunity(npc, disease, untilTick);
        if (index >= 0) _immunities[index] = immunity;
        else _immunities.Insert(~index, immunity);

        CanonicalHashCache.MarkPropertyDirty(nameof(Immunities));
    }

    /// <summary>Descarta as imunidades já vencidas, para o livro não crescer com o tempo.</summary>
    public void PurgeExpiredImmunities()
    {
        long now = CurrentDate.TotalHours;
        if (_immunities.RemoveAll(i => i.UntilTick <= now) > 0)
            CanonicalHashCache.MarkPropertyDirty(nameof(Immunities));
    }

    /// <summary>O NPC está cuidando de algum doente de cama agora.</summary>
    public bool IsCaregiver(NpcId npc)
    {
        if (_infections.Count == 0) return false;
        if (_caregiversVersion != _infectionsVersion)
        {
            var ids = new List<long>();
            foreach (var infection in _infections)
                if (infection is { Symptomatic: true, Bedridden: true, Caregiver: { } who }) ids.Add(who.Value);
            ids.Sort();
            _caregivers = ids.ToArray();
            _caregiversVersion = _infectionsVersion;
        }

        return _caregivers.Length > 0 && Array.BinarySearch(_caregivers, npc.Value) >= 0;
    }

    private void TouchInfections()
    {
        _infectionsVersion++;
        CanonicalHashCache.MarkPropertyDirty(nameof(Infections));
    }

    private int IndexOfInfection(NpcId npc, int disease)
    {
        int at = LowerBound(_infections, npc.Value, disease, static i => (i.Npc.Value, i.Disease));
        return at < _infections.Count && _infections[at].Npc == npc && _infections[at].Disease == disease ? at : ~at;
    }

    private int IndexOfImmunity(NpcId npc, int disease)
    {
        int at = LowerBound(_immunities, npc.Value, disease, static i => (i.Npc.Value, i.Disease));
        return at < _immunities.Count && _immunities[at].Npc == npc && _immunities[at].Disease == disease ? at : ~at;
    }

}
