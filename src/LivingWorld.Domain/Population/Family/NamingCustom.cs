using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Costume de nome declarado pelo cenário ou período para uma cultura (AD-183, bloco opcional <c>Naming</c>). Campo
/// nulo = o que o RNG da cultura sorteia (o padrão do AD-178). <see cref="Lineage"/>: <c>Patrilineal</c> ou <c>Matrilineal</c>;
/// <see cref="DaughterCity"/>: <c>Mother</c> (1ª sílaba da mãe), <c>Founder</c> (nome de quem funda) ou <c>New</c>.</summary>
public sealed record NamingCustom
{
    public int Culture { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Lineage { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SpouseTakesName { get; init; }

    /// <summary>Chance (0–1) de o filho levar o nome de um avô/avó ou do pai morto antes do parto.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AncestorNameChance { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DaughterCity { get; init; }

    public static readonly string[] Lineages = ["Patrilineal", "Matrilineal"];
    public static readonly string[] DaughterCities = ["Mother", "Founder", "New"];

    /// <summary>Valida uma lista de costumes: cultura positiva e sem repetição, linhagem e nome da cidade-filha conhecidos,
    /// chance de homenagem em [0, 1].</summary>
    public static Result<IReadOnlyList<NamingCustom>> Validate(IReadOnlyList<NamingCustom> customs)
    {
        var seen = new HashSet<int>();
        foreach (var c in customs)
        {
            if (c.Culture <= 0) return Result<IReadOnlyList<NamingCustom>>.Fail("Naming.Cultures: Culture deve ser um id de cultura positivo");
            if (!seen.Add(c.Culture)) return Result<IReadOnlyList<NamingCustom>>.Fail($"Naming.Cultures: cultura {c.Culture} repetida");
            if (c.Lineage is { } l && !Lineages.Contains(l))
                return Result<IReadOnlyList<NamingCustom>>.Fail($"Naming.Cultures: Lineage '{l}' deve ser Patrilineal ou Matrilineal");
            if (c.DaughterCity is { } d && !DaughterCities.Contains(d))
                return Result<IReadOnlyList<NamingCustom>>.Fail($"Naming.Cultures: DaughterCity '{d}' deve ser Mother, Founder ou New");
            if (c.AncestorNameChance is { } p && (double.IsNaN(p) || p < 0 || p > 1))
                return Result<IReadOnlyList<NamingCustom>>.Fail("Naming.Cultures: AncestorNameChance deve estar em [0, 1]");
        }
        return Result<IReadOnlyList<NamingCustom>>.Ok(customs.OrderBy(c => c.Culture).ToList());
    }
}
