using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Family;

/// <summary>Parentesco pela genealogia (AD-136): coeficiente de parentesco de Wright <c>r = 2φ</c>, com φ (coancestria)
/// pela recursão clássica — φ(x,x) = 1/2; φ(x,y) = [φ(mãe de x, y) + φ(pai de x, y)] / 2, com x o mais novo dos dois (assim
/// x nunca é ascendente de y). Pais desconhecidos ou fora da memória do mundo valem 0; a recursão vai até
/// <paramref name="depth"/> gerações. Pai e filho, irmãos germanos: 1/2; meio-irmãos, tio e sobrinha, avô e neta: 1/4;
/// primos-irmãos: 1/8; primos de segundo grau: 1/32.</summary>
public static class Kinship
{
    public static double Relatedness(WorldState world, Npc a, Npc b, int depth)
    {
        if (a.Id == b.Id) return 1.0;
        return 2 * Coancestry(world, a, b, depth * 2);
    }

    private static double Coancestry(WorldState world, Npc? x, Npc? y, int budget)
    {
        if (x is null || y is null || budget < 0) return 0;
        if (x.Id == y.Id) return 0.5;
        // x é o mais novo (nascido depois; empate pelo maior id): só ele pode ser descendente do outro
        if (x.BirthDate.TotalHours < y.BirthDate.TotalHours
            || (x.BirthDate.TotalHours == y.BirthDate.TotalHours && x.Id.Value < y.Id.Value))
            (x, y) = (y, x);
        if (x.MotherId is null && x.FatherId is null) return 0;
        double m = x.MotherId is { } mid ? Coancestry(world, world.FindNpc(mid), y, budget - 1) : 0;
        double f = x.FatherId is { } fid ? Coancestry(world, world.FindNpc(fid), y, budget - 1) : 0;
        return (m + f) / 2;
    }

    /// <summary>Tabu de união: parentesco igual ou acima do limite do cenário. <paramref name="lineage"/> guarda, por
    /// pessoa, ela e seus ascendentes conhecidos: sem ascendente comum o parentesco é 0 e a recursão nem roda.</summary>
    public static bool IsTaboo(WorldState world, Npc a, Npc b, Dictionary<long, HashSet<long>>? lineage = null)
    {
        var rules = world.FamilyRules.Unions;
        lineage ??= [];
        var la = LineageOf(world, a, rules.KinshipDepth, lineage);
        var lb = LineageOf(world, b, rules.KinshipDepth, lineage);
        if (!la.Overlaps(lb)) return false;
        return Relatedness(world, a, b, rules.KinshipDepth) >= rules.ForbiddenRelatedness - 1e-9;
    }

    private static HashSet<long> LineageOf(WorldState world, Npc npc, int depth, Dictionary<long, HashSet<long>> cache)
    {
        if (cache.TryGetValue(npc.Id.Value, out var known)) return known;
        var set = new HashSet<long>();
        void Walk(Npc? who, int left)
        {
            if (who is null || !set.Add(who.Id.Value) || left == 0) return;
            if (who.MotherId is { } m) Walk(world.FindNpc(m), left - 1);
            if (who.FatherId is { } f) Walk(world.FindNpc(f), left - 1);
        }
        Walk(npc, depth);
        cache[npc.Id.Value] = set;
        return set;
    }
}
