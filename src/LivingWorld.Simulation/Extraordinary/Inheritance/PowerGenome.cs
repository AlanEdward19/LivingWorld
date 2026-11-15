using System.Runtime.CompilerServices;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Extraordinary.Inheritance;

/// <summary>Um poder herdável: o primeiro dos seus loci no genoma, a genética declarada e a frequência do alelo nos fundadores.
/// <see cref="Ordinal"/> é a posição entre os poderes herdáveis (chave dos sorteios fixos de fenótipo).</summary>
public sealed record PowerLocus(int Ordinal, string PowerId, PowerGenetics Genetics, int FirstLocus, double FounderFrequency);

/// <summary>Genótipo de poder (AD-156): 2 bits por locus (cópias do alelo do poder, 0 a 2; no X do homem, 0 ou 1), até 32 loci
/// num <c>ulong</c>, na ordem dos descritores do cenário (o poligênico ocupa vários). O fundador (sem concepção simulada) tem os
/// alelos sorteados da semente pela frequência do cenário, cada um independente (Hardy-Weinberg), lidos na hora; quem nasce no
/// mundo recebe de cada pai um dos dois alelos com 50% (1ª lei de Mendel), loci independentes (2ª lei), com mutação nova rara.</summary>
public static class PowerGenome
{
    public const int MaxLoci = 32;
    public const string InheritPurpose = "power-genome";
    private const string FounderPurpose = "power-genome-founder";

    private sealed record Cached(IReadOnlyList<PowerDescriptor> Source, PowerInheritanceRules? Rules, double Prevalence,
        IReadOnlyList<PowerLocus> Loci);

    private static readonly ConditionalWeakTable<ExtraordinaryScenarioData, Cached> Layouts = new();

    /// <summary>Loci dos poderes herdáveis; descritor que não cabe nos 32 loci não é herdável (só por evento ou autoria).</summary>
    public static IReadOnlyList<PowerLocus> Layout(ExtraordinaryScenarioData data)
    {
        if (Layouts.TryGetValue(data, out var hit) && ReferenceEquals(hit.Source, data.Descriptors)
            && ReferenceEquals(hit.Rules, data.InheritanceRules) && hit.Prevalence == data.Prevalence)
            return hit.Loci;
        var rules = PowerInheritanceRules.Resolve(data.InheritanceRules);
        var fitting = new List<(string Id, PowerGenetics Genetics, int First)>();
        int next = 0;
        foreach (var descriptor in data.Descriptors)
        {
            var genetics = rules.For(descriptor.Id);
            if (next + genetics.LociCount > MaxLoci) continue;
            fitting.Add((descriptor.Id, genetics, next));
            next += genetics.LociCount;
        }

        double perPower = fitting.Count == 0 ? 0 : data.Prevalence / fitting.Count;
        var loci = fitting.Select((f, i) => new PowerLocus(i, f.Id, f.Genetics, f.First, f.Genetics.FounderFrequency(perPower)))
            .ToList();
        Layouts.AddOrUpdate(data, new Cached(data.Descriptors, data.InheritanceRules, data.Prevalence, loci));
        return loci;
    }

    public static PowerLocus? Find(WorldState world, string powerId)
    {
        foreach (var locus in Layout(world.Extraordinary))
            if (string.Equals(locus.PowerId, powerId, StringComparison.Ordinal)) return locus;
        return null;
    }

    public static int Copies(ulong genome, int locus) => (int)((genome >> (2 * locus)) & 3UL);

    public static ulong With(ulong genome, int locus, int copies) =>
        (genome & ~(3UL << (2 * locus))) | ((ulong)copies << (2 * locus));

    /// <summary>Genótipo da pessoa: o guardado de quem nasceu no mundo, ou o sorteio fixo do fundador.</summary>
    public static ulong Of(WorldState world, Npc npc) =>
        npc.PowerGenome != 0 || npc.GestationAtBirthDays is not null ? npc.PowerGenome : Founder(world, npc.Id, npc.Sex);

    /// <summary>Fundador: dois sorteios por locus pela frequência (o homem usa só o primeiro no X). Sem sexo conhecido (membro
    /// do pool agregado) o X fica vazio.</summary>
    public static ulong Founder(WorldState world, NpcId id, Sex? sex)
    {
        var layout = Layout(world.Extraordinary);
        if (layout.Count == 0) return 0;
        ulong state = world.Rng.StreamStateFor(FounderPurpose, id.Value);
        ulong genome = 0;
        foreach (var locus in layout)
            for (int k = 0; k < locus.Genetics.LociCount; k++)
            {
                int a1 = WorldRng.NextDouble(ref state) < locus.FounderFrequency ? 1 : 0;
                int a2 = WorldRng.NextDouble(ref state) < locus.FounderFrequency ? 1 : 0;
                int copies = !locus.Genetics.IsXLinked ? a1 + a2 : sex switch { Sex.Male => a1, Sex.Female => a1 + a2, _ => 0 };
                genome = With(genome, locus.FirstLocus + k, copies);
            }
        return genome;
    }

    /// <summary>Genótipo do filho. A mãe dá um dos seus dois alelos; o pai, um dos dois no autossomo e o seu único X à filha
    /// (ao filho dá o Y). Pai desconhecido dá um alelo da população. O alelo sem o poder muta com a taxa do poder.</summary>
    public static ulong Inherit(IReadOnlyList<PowerLocus> layout, ulong mother, ulong? father, Sex childSex, WorldRng rng)
    {
        ulong child = 0;
        foreach (var locus in layout)
        {
            double mu = locus.Genetics.MutationRate;
            for (int k = 0; k < locus.Genetics.LociCount; k++)
            {
                int index = locus.FirstLocus + k;
                int fromMother = Gamete(Copies(mother, index), mu, rng);
                int fromFather = locus.Genetics.IsXLinked && childSex == Sex.Male ? 0
                    : father is not { } f ? (rng.NextDouble() < locus.FounderFrequency ? 1 : 0)
                    : locus.Genetics.IsXLinked ? Mutate(Copies(f, index) > 0 ? 1 : 0, mu, rng)
                    : Gamete(Copies(f, index), mu, rng);
                child = With(child, index, fromMother + fromFather);
            }
        }
        return child;
    }

    private static int Gamete(int copies, double mutationRate, WorldRng rng)
    {
        double u = rng.NextDouble();
        return Mutate(copies switch { 0 => 0, 2 => 1, _ => u < 0.5 ? 1 : 0 }, mutationRate, rng);
    }

    private static int Mutate(int allele, double mutationRate, WorldRng rng) =>
        allele == 0 && mutationRate > 0 && rng.NextDouble() < mutationRate ? 1 : allele;

    /// <summary>Soma das cópias dos loci do poder (o escore do poligênico; nos outros, as cópias do único locus).</summary>
    public static int Dose(PowerLocus locus, ulong genome)
    {
        int sum = 0;
        for (int k = 0; k < locus.Genetics.LociCount; k++) sum += Copies(genome, locus.FirstLocus + k);
        return sum;
    }

    /// <summary>O genótipo dá o poder? <paramref name="environment"/> é o desvio do ambiente (só no poligênico).</summary>
    public static bool IsAffected(PowerLocus locus, ulong genome, Sex sex, double environment)
    {
        int dose = Dose(locus, genome);
        return locus.Genetics.Mode switch
        {
            PowerInheritanceMode.Dominant or PowerInheritanceMode.XLinkedDominant => dose >= 1,
            PowerInheritanceMode.Recessive => dose == 2,
            PowerInheritanceMode.XLinkedRecessive => sex == Sex.Male ? dose >= 1 : dose == 2,
            _ => dose + locus.Genetics.PolygenicEnvironmentStdDev * environment >= locus.Genetics.PolygenicThreshold,
        };
    }
}
