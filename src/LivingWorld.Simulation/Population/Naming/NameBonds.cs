using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>Laços que o nome cria (AD-178), lidos do nome e da genealogia, sem estado novo. Xará: mesmo sexo, mesmo nome
/// próprio, e um é pai/mãe ou avô/avó do outro (a homenagem do costume, ou o acaso que dá no mesmo). Linhagem: mesmo nome
/// de família; quem trocou de nome ao casar passa a contar na linhagem do cônjuge. Nome legado (uma palavra) não tem nenhum.
/// Comparação por trecho do nome, sem alocar.</summary>
public static class NameBonds
{
    public static bool SameLineage(Npc a, Npc b) => SameLineage(a.Name, b.Name);

    public static bool SameLineage(string a, string b)
    {
        int ca = a.LastIndexOf(' '), cb = b.LastIndexOf(' ');
        return ca > 0 && cb > 0 && a.AsSpan(ca + 1).SequenceEqual(b.AsSpan(cb + 1));
    }

    public static bool SameGivenName(Npc a, Npc b)
    {
        int ca = a.Name.LastIndexOf(' '), cb = b.Name.LastIndexOf(' ');
        return ca > 0 && cb > 0 && a.Name.AsSpan(0, ca).SequenceEqual(b.Name.AsSpan(0, cb));
    }

    public static bool IsNamesake(WorldState world, Npc a, Npc b)
    {
        if (a.Id == b.Id || a.Sex != b.Sex || !SameGivenName(a, b)) return false;
        var (young, old) = a.BirthDate.TotalHours >= b.BirthDate.TotalHours ? (a, b) : (b, a);
        if (young.MotherId == old.Id || young.FatherId == old.Id) return true;
        foreach (var parentId in new[] { young.MotherId, young.FatherId })
            if (parentId is { } id && world.FindNpc(id) is { } parent && (parent.MotherId == old.Id || parent.FatherId == old.Id))
                return true;
        return false;
    }

    /// <summary>Algum dos <paramref name="members"/> tem <paramref name="helper"/> por xará.</summary>
    public static bool NamesakeOfAny(WorldState world, Npc helper, IReadOnlyList<Npc> members)
    {
        for (int i = 0; i < members.Count; i++)
            if (IsNamesake(world, helper, members[i])) return true;
        return false;
    }

    public static bool LineageOfAny(Npc helper, IReadOnlyList<Npc> members)
    {
        for (int i = 0; i < members.Count; i++)
            if (SameLineage(helper, members[i])) return true;
        return false;
    }
}
