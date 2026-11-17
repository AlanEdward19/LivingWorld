using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Population.Naming;

/// <summary>De quem o filho herda o nome de família (AD-178).</summary>
public enum Lineage
{
    Patrilineal,
    Matrilineal,
}

/// <summary>Fonologia e costume de nome de uma cultura (AD-178). Nada é lista de nomes do mundo real: cada cultura escolhe,
/// pelo RNG semeado do mundo (stream <c>culture-naming</c> da própria cultura), um punhado das mesmas sílabas de onde saem os
/// nomes de cidade (<see cref="CityNameGenerator"/>), as terminações de nome feminino, masculino e de família, a linhagem do
/// nome de família, se o cônjuge adota o nome da outra linhagem e a chance de o filho levar o nome de um avô/avó ou de um
/// parente morto. Mesmo mundo, mesma cultura: mesmo resultado; outro mundo: outra língua. Recalculado por pessoa nomeada
/// (barato, não vai ao snapshot).</summary>
public sealed record CultureNaming(
    IReadOnlyList<string> Syllables, IReadOnlyList<string> FemaleEndings, IReadOnlyList<string> MaleEndings,
    IReadOnlyList<string> FamilySuffixes, Lineage Lineage, bool SpouseTakesName, double AncestorNameChance)
{
    /// <summary>Sufixos de lugar da cultura (nome de cidade); vazio = só sílabas.</summary>
    public IReadOnlyList<string> PlaceSuffixes { get; init; } = [""];

    /// <summary>Costume: a cidade-filha leva a primeira sílaba do nome da mãe (senão, nome novo).</summary>
    public bool DaughterTakesMotherName { get; init; }

    /// <summary>Quantas sílabas da raiz comum cada cultura usa.</summary>
    public const int SyllablesPerCulture = 9;

    // Morfemas de terminação (não nomes): cada cultura fica com três de cada.
    private static readonly string[] FemalePool = ["a", "ia", "e", "ela", "ina", "is", "eth", "ra", "ena", "ys"];
    private static readonly string[] MalePool = ["o", "an", "er", "us", "im", "or", "ek", "in", "ald", "ur"];
    private static readonly string[] FamilyPool = ["", "en", "ar", "ick", "ow", "ith", "ane", "os"];
    private static readonly string[] PlacePool = ["", "a", "on", "um", "ast", "ey", "is", "or"];

    /// <summary>Parte das culturas em que a cidade-filha herda a primeira sílaba da mãe.</summary>
    public const double DaughterTakesMotherNameShare = 0.4;

    /// <summary>Padrão declarado: 70% das culturas são patrilineares; metade casa adotando o nome; a chance de homenagem fica
    /// entre 15% e 50%.</summary>
    public const double PatrilinealShare = 0.7;
    public const double SpouseTakesNameShare = 0.5;
    public const double MinAncestorNameChance = 0.15;
    public const double MaxAncestorNameChance = 0.5;

    public static CultureNaming Of(WorldState world, CultureId culture) =>
        Derive(world.Rng.StreamFor("culture-naming", culture.Id));

    public static CultureNaming Derive(WorldRng rng)
    {
        var syllables = Pick(CityNameGenerator.Syllables, SyllablesPerCulture, rng);
        var female = Pick(FemalePool, 3, rng);
        var male = Pick(MalePool, 3, rng);
        var family = Pick(FamilyPool, 3, rng);
        var lineage = rng.NextDouble() < PatrilinealShare ? Lineage.Patrilineal : Lineage.Matrilineal;
        bool spouseTakes = rng.NextDouble() < SpouseTakesNameShare;
        double ancestor = MinAncestorNameChance + rng.NextDouble() * (MaxAncestorNameChance - MinAncestorNameChance);
        // AD-178, lugar: sorteado depois do resto para não mudar o nome das pessoas já sorteado antes
        var place = Pick(PlacePool, 2, rng);
        bool daughter = rng.NextDouble() < DaughterTakesMotherNameShare;
        return new CultureNaming(syllables, female, male, family, lineage, spouseTakes, ancestor)
        {
            PlaceSuffixes = place,
            DaughterTakesMotherName = daughter,
        };
    }

    /// <summary>Nome próprio novo: uma ou duas sílabas da cultura e a terminação do sexo. Uma palavra só.</summary>
    public string NewGivenName(Sex sex, WorldRng rng)
    {
        int count = rng.NextDouble() < 0.55 ? 1 : 2;
        var endings = sex == Sex.Female ? FemaleEndings : MaleEndings;
        return Join(Stem(count, rng), endings[Index(rng, endings.Count)]);
    }

    /// <summary>Nome de família novo: duas sílabas da cultura e um sufixo de família (pode ser vazio). Uma palavra só.</summary>
    public string NewFamilyName(WorldRng rng) => Join(Stem(2, rng), FamilySuffixes[Index(rng, FamilySuffixes.Count)]);

    /// <summary>Nome de lugar: duas ou três sílabas da cultura e um sufixo de lugar; com o costume, a cidade-filha começa pela
    /// primeira sílaba da mãe (<paramref name="motherName"/>).</summary>
    public string NewPlaceName(WorldRng rng, string? motherName = null)
    {
        string? head = DaughterTakesMotherName && !string.IsNullOrEmpty(motherName) ? FirstSyllable(motherName) : null;
        int count = 2 + Index(rng, 2);
        string stem = head is null ? Stem(count, rng) : head + Stem(count - 1, rng).ToLowerInvariant();
        return Join(stem, PlaceSuffixes[Index(rng, PlaceSuffixes.Count)]);
    }

    private static string FirstSyllable(string name)
    {
        string best = "";
        foreach (var syllable in CityNameGenerator.Syllables)
            if (syllable.Length > best.Length && name.StartsWith(syllable, StringComparison.Ordinal)) best = syllable;
        return best.Length > 0 ? best : name[..Math.Min(3, name.Length)];
    }

    private string Stem(int count, WorldRng rng)
    {
        var stem = new System.Text.StringBuilder();
        int previous = -1;
        for (int i = 0; i < count; i++)
        {
            int index = Index(rng, Syllables.Count);
            if (index == previous) index = (index + 1) % Syllables.Count; // sem "Osos" nem "Dundun"
            previous = index;
            string syllable = Syllables[index];
            stem.Append(i == 0 ? syllable : syllable.ToLowerInvariant());
        }
        return stem.ToString();
    }

    // vogal com vogal ganha um "r" de ligação ("Qua" + "a" = "Quara")
    private static string Join(string stem, string ending) =>
        ending.Length > 0 && IsVowel(stem[^1]) && IsVowel(ending[0]) ? stem + "r" + ending : stem + ending;

    private static bool IsVowel(char c) => "aeiouyAEIOUY".IndexOf(c) >= 0;

    private static int Index(WorldRng rng, int count) => Math.Min(count - 1, (int)(rng.NextDouble() * count));

    /// <summary>Embaralhamento parcial (Fisher-Yates) de uma cópia: <paramref name="take"/> itens distintos, ordem sorteada.</summary>
    private static string[] Pick(string[] pool, int take, WorldRng rng)
    {
        var copy = (string[])pool.Clone();
        take = Math.Min(take, copy.Length);
        for (int i = 0; i < take; i++)
        {
            int j = i + Index(rng, copy.Length - i);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy[..take];
    }
}
