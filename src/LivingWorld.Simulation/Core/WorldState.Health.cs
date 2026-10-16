using LivingWorld.Domain.Health;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState: infecções e imunidades (disease-epidemics).
public sealed partial class WorldState
{
    public bool IsInfected(NpcId npc, int disease) => IndexOfInfection(npc, disease) >= 0;

    public bool IsImmune(NpcId npc, int disease)
    {
        int index = IndexOfImmunity(npc, disease);
        return index >= 0 && _immunities[index].UntilTick > CurrentDate.TotalHours;
    }

    public void AddInfection(NpcId npc, int disease, long endTick)
    {
        if (IsInfected(npc, disease)) return;
        var created = new Infection(npc, disease, endTick);
        int at = _infections.BinarySearch(created, Comparer<Infection>.Create(InfectionOrder));
        _infections.Insert(at < 0 ? ~at : at, created);
        CanonicalHashCache.MarkPropertyDirty(nameof(Infections));
    }

    public void RemoveInfection(NpcId npc, int disease)
    {
        int index = IndexOfInfection(npc, disease);
        if (index < 0) return;
        _infections.RemoveAt(index);
        CanonicalHashCache.MarkPropertyDirty(nameof(Infections));
    }

    public void SetImmunity(NpcId npc, int disease, long untilTick)
    {
        int index = IndexOfImmunity(npc, disease);
        var immunity = new Immunity(npc, disease, untilTick);
        if (index >= 0) _immunities[index] = immunity;
        else
        {
            int at = _immunities.BinarySearch(immunity, Comparer<Immunity>.Create((a, b) => CompareKey(a.Npc, a.Disease, b.Npc, b.Disease)));
            _immunities.Insert(at < 0 ? ~at : at, immunity);
        }

        CanonicalHashCache.MarkPropertyDirty(nameof(Immunities));
    }

    /// <summary>Descarta as imunidades já vencidas, para o livro não crescer com o tempo.</summary>
    public void PurgeExpiredImmunities()
    {
        long now = CurrentDate.TotalHours;
        if (_immunities.RemoveAll(i => i.UntilTick <= now) > 0)
            CanonicalHashCache.MarkPropertyDirty(nameof(Immunities));
    }

    private int IndexOfInfection(NpcId npc, int disease)
    {
        int at = _infections.BinarySearch(new Infection(npc, disease, 0), Comparer<Infection>.Create(InfectionOrder));
        return at;
    }

    private int IndexOfImmunity(NpcId npc, int disease) =>
        _immunities.FindIndex(i => i.Npc == npc && i.Disease == disease);

    private static int InfectionOrder(Infection a, Infection b) => CompareKey(a.Npc, a.Disease, b.Npc, b.Disease);

    private static int CompareKey(NpcId a, int diseaseA, NpcId b, int diseaseB)
    {
        int byNpc = a.Value.CompareTo(b.Value);
        return byNpc != 0 ? byNpc : diseaseA.CompareTo(diseaseB);
    }
}
