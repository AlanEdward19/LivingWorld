using System.Reflection;
using LivingWorld.Domain.Population;

namespace LivingWorld.Domain.Behavior;

/// <summary>Peso de personalidade no utility AI: <c>peso = 1 + (traço/100 −
/// 0.5) × influência[traço][ação]</c>. A fórmula e a tabela de influência são o modelo de
/// decisão em si (algoritmo), não conteúdo de cenário — por isso vivem em código, nunca em
/// JSON (mesmo status que a fórmula de utilidade).</summary>
public static class PersonalityWeighting
{
    /// <summary>Coeficiente de influência de cada traço sobre a ação que ele modula. Todo
    /// traço de <see cref="Personality"/> precisa de ao menos uma entrada aqui — ver teste de
    /// cobertura por reflexão.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ActionType, double>> Influence =
        new Dictionary<string, IReadOnlyDictionary<ActionType, double>>
        {
            [nameof(Personality.Conscientiousness)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0, [ActionType.Steal] = -1.0 },
            [nameof(Personality.Ambition)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0 },
            [nameof(Personality.Extroversion)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
            [nameof(Personality.Openness)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
            [nameof(Personality.Altruism)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0, [ActionType.Steal] = -1.0 },
            [nameof(Personality.Loyalty)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0 },
            [nameof(Personality.Impulsivity)] = new Dictionary<ActionType, double>
            {
                [ActionType.Idle] = 1.0,
                [ActionType.Work] = -1.0,
                [ActionType.Steal] = 1.0,
            },
            [nameof(Personality.RiskAversion)] = new Dictionary<ActionType, double> { [ActionType.Travel] = -1.0, [ActionType.Steal] = -1.0 },
            [nameof(Personality.EmotionalStability)] = new Dictionary<ActionType, double> { [ActionType.Idle] = -1.0 },
            [nameof(Personality.Agreeableness)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
        };

    /// <summary>Todo traço declarado em <see cref="Personality"/> — usado pelo teste de
    /// cobertura por reflexão (falha se um traço novo não tiver entrada em
    /// <see cref="Influence"/>).</summary>
    public static IReadOnlyList<string> AllTraitNames { get; } =
        typeof(Personality).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

    public static bool HasInfluenceEntry(string traitName) => Influence.ContainsKey(traitName);

    /// <summary>Termos (acessor do traço, coeficiente) de cada ação, já resolvidos, na mesma ordem de
    /// <see cref="AllTraitNames"/> — a soma em ponto flutuante é idêntica à varredura por nome, sem os
    /// lookups de string por traço a cada decisão.</summary>
    private static readonly (Func<Personality, int> Trait, double Coefficient)[][] TermsByAction = BuildTerms();

    private static (Func<Personality, int>, double)[][] BuildTerms()
    {
        int size = Enum.GetValues<ActionType>().Max(a => (int)a) + 1;
        var terms = new (Func<Personality, int>, double)[size][];
        for (int action = 0; action < size; action++)
        {
            var list = new List<(Func<Personality, int>, double)>();
            foreach (var traitName in AllTraitNames)
            {
                if (!Influence.TryGetValue(traitName, out var actionInfluence)) continue;
                if (!actionInfluence.TryGetValue((ActionType)action, out double coefficient)) continue;

                list.Add((AccessorOf(traitName), coefficient));
            }
            terms[action] = list.ToArray();
        }
        return terms;
    }

    public static double WeightOf(Personality personality, ActionType action)
    {
        double weight = 1.0;
        int index = (int)action;
        if ((uint)index >= (uint)TermsByAction.Length)
            return weight;

        foreach (var (trait, coefficient) in TermsByAction[index])
            weight += (trait(personality) / 100.0 - 0.5) * coefficient;
        return weight;
    }

    /// <summary>Acesso direto ao traço por nome — <see cref="AllTraitNames"/> e o teste de
    /// cobertura por reflexão continuam pegando traço novo sem entrada aqui (o <c>switch</c>
    /// reprova em runtime), mas o caminho quente (um `WeightOf` por ação por NPC vivo por tick
    /// Hourly) deixa de pagar `PropertyInfo.GetValue` reflexivo — medido como o maior custo do
    /// sistema em população grande (ver STATE.md).</summary>
    private static Func<Personality, int> AccessorOf(string traitName) => traitName switch
    {
        nameof(Personality.Extroversion) => p => p.Extroversion,
        nameof(Personality.Agreeableness) => p => p.Agreeableness,
        nameof(Personality.Conscientiousness) => p => p.Conscientiousness,
        nameof(Personality.EmotionalStability) => p => p.EmotionalStability,
        nameof(Personality.Openness) => p => p.Openness,
        nameof(Personality.Ambition) => p => p.Ambition,
        nameof(Personality.Loyalty) => p => p.Loyalty,
        nameof(Personality.Altruism) => p => p.Altruism,
        nameof(Personality.Impulsivity) => p => p.Impulsivity,
        nameof(Personality.RiskAversion) => p => p.RiskAversion,
        _ => _ => throw new ArgumentOutOfRangeException(nameof(traitName), traitName, "traço sem acesso direto declarado"),
    };
}
