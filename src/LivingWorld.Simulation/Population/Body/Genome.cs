using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Body;

/// <summary>Doenças genéticas mendelianas (AD-139). Genótipo em 2 bits por locus (cópias do alelo de doença: 0, 1 ou 2).
/// Fundador (quem não nasceu de uma concepção simulada: sem gestação registrada) tem o genótipo sorteado da semente pela frequência de cada
/// locus, lido na hora; quem nasce no mundo recebe um alelo de cada pai na concepção, com mutação nova rara, e o guarda.
/// Afetado: uma cópia na dominante, duas na recessiva; o portador de recessiva não tem sintoma.</summary>
public static class Genome
{
    public static HeritableDisorderRules Rules(WorldState world) => world.BodyRules.Disorders;

    public static int Copies(uint genome, int locus) => (int)((genome >> (2 * locus)) & 3u);

    private static uint With(uint genome, int locus, int copies) =>
        (genome & ~(3u << (2 * locus))) | ((uint)copies << (2 * locus));

    /// <summary>Genótipo da pessoa: o guardado, ou o sorteio fixo do fundador.</summary>
    public static uint Of(WorldState world, Npc npc)
    {
        // nascido no mundo (tem gestação registrada): o genótipo herdado e guardado; senão, fundador (população inicial,
        // imigrante, instanciado), inclusive os filhos da família inicial, que não nasceram de uma concepção simulada
        if (npc.Genome != 0 || npc.GestationAtBirthDays is not null)
            return npc.Genome;
        var rules = Rules(world);
        if (!rules.Enabled) return 0;
        ulong s = world.Rng.StreamStateFor("genome-founder", npc.Id.Value); // lido todo mês pela mortalidade: sem alocar
        uint g = 0;
        for (int i = 0; i < rules.Loci.Count; i++)
        {
            double q = rules.Loci[i].FounderAlleleFrequency;
            int copies = (WorldRng.NextDouble(ref s) < q ? 1 : 0) + (WorldRng.NextDouble(ref s) < q ? 1 : 0);
            g = With(g, i, copies);
        }
        return g;
    }

    /// <summary>Genótipo do filho: de cada pai sai um dos dois alelos (segregação), e o alelo sadio pode mutar.</summary>
    public static uint Inherit(WorldState world, Npc mother, Npc? father, WorldRng rng)
    {
        var rules = Rules(world);
        if (!rules.Enabled) return 0;
        uint m = Of(world, mother), f = father is null ? 0 : Of(world, father);
        uint child = 0;
        for (int i = 0; i < rules.Loci.Count; i++)
        {
            var locus = rules.Loci[i];
            int copies = Gamete(Copies(m, i), locus.MutationRate, rng) + Gamete(Copies(f, i), locus.MutationRate, rng);
            child = With(child, i, copies);
        }
        return child;
    }

    private static int Gamete(int parentCopies, double mutationRate, WorldRng rng)
    {
        double u = rng.NextDouble();
        int allele = parentCopies switch { 0 => 0, 2 => 1, _ => u < 0.5 ? 1 : 0 };
        if (allele == 0 && rng.NextDouble() < mutationRate) allele = 1;
        return allele;
    }

    public static bool IsAffected(DisorderLocus locus, int copies) => locus.Dominant ? copies >= 1 : copies == 2;

    /// <summary>Risco de morte multiplicado pelos loci em que a pessoa é afetada, a partir da idade de início.</summary>
    public static double HazardMultiplier(WorldState world, Npc npc, double ageYears)
    {
        var rules = Rules(world);
        if (!rules.Enabled) return 1.0;
        uint g = Of(world, npc);
        if (g == 0) return 1.0;
        double m = 1.0;
        for (int i = 0; i < rules.Loci.Count; i++)
            if (IsAffected(rules.Loci[i], Copies(g, i)) && ageYears >= rules.Loci[i].OnsetAgeYears)
                m *= rules.Loci[i].HazardMultiplier;
        return m;
    }

    /// <summary>Fertilidade multiplicada pelos loci em que a pessoa é afetada.</summary>
    public static double FertilityMultiplier(WorldState world, Npc npc)
    {
        var rules = Rules(world);
        if (!rules.Enabled) return 1.0;
        uint g = Of(world, npc);
        if (g == 0) return 1.0;
        double m = 1.0;
        for (int i = 0; i < rules.Loci.Count; i++)
            if (IsAffected(rules.Loci[i], Copies(g, i))) m *= rules.Loci[i].FertilityMultiplier;
        return m;
    }
}
