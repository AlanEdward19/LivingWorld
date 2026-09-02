using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Founding;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Cities;

/// <summary>Nome determinístico por composição de sílabas (Fase 15.1, T44; ADR-0013 — "sem LLM,
/// o motor gera um nome determinístico por composição", listado ali como fallback obrigatório
/// de gramática procedural). Usado quando o cenário não autora um nome para a cidade e quando
/// <see cref="SettlementFoundingSystem"/> funda uma nova durante a simulação — stream de RNG
/// dedicado (<c>"city-naming"</c>), separado de <c>"city-founding"</c> para não deslocar a
/// sequência de <see cref="WorldState.NextCityId"/>.</summary>
public static class CityNameGenerator
{
    private static readonly string[] Syllables =
    [
        "Al", "Bar", "Cor", "Dun", "El", "Fen", "Gal", "Hol", "Is", "Kar",
        "Lor", "Mor", "Nor", "Os", "Pel", "Qua", "Ren", "Sil", "Tor", "Ur", "Val", "Wyn",
    ];

    public static string Generate(WorldState world)
    {
        var rng = world.Rng.Stream("city-naming");
        int syllableCount = 2 + NextIndex(rng, 2);

        var name = new System.Text.StringBuilder();
        for (int i = 0; i < syllableCount; i++)
            name.Append(Syllables[NextIndex(rng, Syllables.Length)]);

        return name.ToString();
    }

    private static int NextIndex(WorldRng rng, int exclusiveUpperBound) =>
        (int)(rng.NextDouble() * exclusiveUpperBound);
}
