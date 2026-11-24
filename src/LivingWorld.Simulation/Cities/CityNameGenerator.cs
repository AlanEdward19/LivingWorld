using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Naming;

namespace LivingWorld.Simulation.Cities;

/// <summary>Nome determinístico por composição de sílabas ("sem LLM, o motor gera um nome determinístico por composição",
/// fallback obrigatório de gramática procedural). Usado quando o cenário não autora um nome para a cidade e quando
/// <see cref="SettlementFoundingSystem"/> funda uma nova durante a simulação — stream de RNG dedicado (<c>"city-naming"</c>),
/// separado de <c>"city-founding"</c> para não deslocar a sequência de <see cref="WorldState.NextCityId"/>.
/// AD-178: o nome sai da língua da cultura que funda (<see cref="CultureNaming"/>: as sílabas dela e o sufixo de lugar dela),
/// a mesma de onde sai o nome do povo; a cidade-filha, se a cultura tem o costume, leva a primeira sílaba da mãe.</summary>
public static class CityNameGenerator
{
    internal static readonly string[] Syllables = // raiz comum: o nome das pessoas sai destas mesmas sílabas (AD-178)
    [
        "Al", "Bar", "Cor", "Dun", "El", "Fen", "Gal", "Hol", "Is", "Kar",
        "Lor", "Mor", "Nor", "Os", "Pel", "Qua", "Ren", "Sil", "Tor", "Ur", "Val", "Wyn",
    ];

    /// <summary>Nome de cidade na língua de <paramref name="culture"/>; <paramref name="mother"/> é a cidade de onde saem os
    /// fundadores (nulo na semeadura).</summary>
    public static string Generate(WorldState world, CultureId culture, City? mother = null) =>
        CultureNaming.Of(world, culture).NewPlaceName(world.Rng.Stream("city-naming"), mother?.Name);

    /// <summary>AD-183: nome da cidade-filha pelo costume da cultura — o da mãe, o do fundador (<paramref name="founder"/>, se
    /// houver) ou novo — e qual costume valeu (<c>mae</c>, <c>fundador</c>, <c>novo</c>).</summary>
    public static (string Name, string Custom) GenerateDaughter(WorldState world, CultureId culture, City mother, Npc? founder)
    {
        var naming = CultureNaming.Of(world, culture);
        var rng = world.Rng.Stream("city-naming");
        int cut = founder?.Name.LastIndexOf(' ') ?? -1;
        if (!naming.DaughterTakesMotherName && naming.DaughterHonorsFounder && cut > 0)
            return (naming.PlaceNameAfter(founder!.Name[..cut], rng), "fundador");
        return (naming.NewPlaceName(rng, mother.Name), naming.DaughterTakesMotherName ? "mae" : "novo");
    }

    /// <summary>A cultura de quem sai da cidade-mãe para fundar: a mais comum entre os vivos dela (desempate pelo menor id de
    /// cultura); sem ninguém, a primeira do catálogo.</summary>
    public static CultureId FoundingCulture(WorldState world, City mother)
    {
        var counts = new SortedDictionary<int, int>();
        foreach (var npc in world.AliveNpcIndex.Alive)
            if (npc.City == mother.Id) counts[npc.Culture.Id] = counts.GetValueOrDefault(npc.Culture.Id) + 1;
        if (counts.Count > 0) return new CultureId(counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key);
        return world.PopulationCatalog.CultureIds.Count > 0 ? new CultureId(world.PopulationCatalog.CultureIds.Order().First()) : default;
    }
}
