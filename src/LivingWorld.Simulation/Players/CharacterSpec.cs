using LivingWorld.Domain.Population;

namespace LivingWorld.Simulation.Players;

/// <summary>Os dez traços de personalidade que o jogador quis fixar (AD-197t); o que ficar nulo o motor sorteia pelo mundo.</summary>
public sealed record CharacterTraits(
    int? Extroversion = null, int? Agreeableness = null, int? Conscientiousness = null, int? EmotionalStability = null,
    int? Openness = null, int? Ambition = null, int? Loyalty = null, int? Altruism = null, int? Impulsivity = null, int? RiskAversion = null)
{
    internal Personality Over(Personality drawn) => new(
        Extroversion ?? drawn.Extroversion, Agreeableness ?? drawn.Agreeableness, Conscientiousness ?? drawn.Conscientiousness,
        EmotionalStability ?? drawn.EmotionalStability, Openness ?? drawn.Openness, Ambition ?? drawn.Ambition, Loyalty ?? drawn.Loyalty,
        Altruism ?? drawn.Altruism, Impulsivity ?? drawn.Impulsivity, RiskAversion ?? drawn.RiskAversion);

    internal IEnumerable<(string Name, int? Value)> All() =>
    [
        ("Extroversion", Extroversion), ("Agreeableness", Agreeableness), ("Conscientiousness", Conscientiousness),
        ("EmotionalStability", EmotionalStability), ("Openness", Openness), ("Ambition", Ambition), ("Loyalty", Loyalty),
        ("Altruism", Altruism), ("Impulsivity", Impulsivity), ("RiskAversion", RiskAversion),
    ];
}

/// <summary>A aparência que o jogador quis (AD-197t): altura em metros e os genes em [0,100] (pele, cabelo, ondulação, corpo =
/// porte/arcabouço, olhos, rosto) e o traje (0 a <see cref="Appearance.OutfitCount"/> − 1). O que ficar nulo o motor sorteia.</summary>
public sealed record CharacterLook(
    double? HeightM = null, int? SkinTone = null, int? HairTone = null, int? HairCurl = null, int? Build = null,
    int? EyeTone = null, int? FaceShape = null, int? Outfit = null);

/// <summary>O lugar da pessoa na cidade: <see cref="Newcomer"/> (recém-chegado, de fora do mundo ou de <see cref="FromCityId"/>) ou
/// um laço com <see cref="RelativeId"/>: <see cref="Child"/> (filho/filha dele e do cônjuge, se vivo), <see cref="Sibling"/> (irmão:
/// mesmos pais), <see cref="Spouse"/> (cônjuge: ele tem de estar livre e aceitar) ou <see cref="Relative"/> (parente agregado à casa,
/// com vínculo, sem laço genealógico).</summary>
public sealed record CharacterRole(string Kind = CharacterRole.Newcomer, long? RelativeId = null, Guid? FromCityId = null)
{
    public const string Newcomer = "newcomer", Child = "child", Sibling = "sibling", Spouse = "spouse", Relative = "relative";
    public static readonly IReadOnlyList<string> Kinds = [Newcomer, Child, Sibling, Spouse, Relative];
}

/// <summary>Tudo o que o jogador escolhe ao criar a pessoa (AD-197t): cidade e o resto opcional. O que não vier o motor sorteia pelo
/// mundo, pela chave (jogador, <see cref="Seed"/>): a mesma escolha dá a mesma pessoa na prévia, ao criar e no replay. Vai inteira
/// para o <c>players.log</c>.</summary>
public sealed record CharacterSpec(
    Guid CityId, string? Name = null, Sex? Sex = null, int? AgeYears = null, long Seed = 0, int? ProfessionId = null,
    CharacterTraits? Traits = null, int? Health = null, long? Money = null, CharacterLook? Look = null, CharacterRole? Role = null);
