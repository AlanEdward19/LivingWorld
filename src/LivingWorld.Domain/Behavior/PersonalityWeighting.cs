using System.Reflection;

namespace LivingWorld.Domain;

/// <summary>Peso de personalidade no utility AI (Fase 4, task 5): <c>peso = 1 + (traço/100 −
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
            [nameof(Personality.Conscientiousness)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0 },
            [nameof(Personality.Ambition)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0 },
            [nameof(Personality.Extroversion)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
            [nameof(Personality.Openness)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
            [nameof(Personality.Altruism)] = new Dictionary<ActionType, double> { [ActionType.Socialize] = 1.0 },
            [nameof(Personality.Loyalty)] = new Dictionary<ActionType, double> { [ActionType.Work] = 1.0 },
            [nameof(Personality.Impulsivity)] = new Dictionary<ActionType, double>
            {
                [ActionType.Idle] = 1.0,
                [ActionType.Work] = -1.0,
            },
            [nameof(Personality.RiskAversion)] = new Dictionary<ActionType, double> { [ActionType.Travel] = -1.0 },
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

    public static double WeightOf(Personality personality, ActionType action)
    {
        double weight = 1.0;
        foreach (var traitName in AllTraitNames)
        {
            if (!Influence.TryGetValue(traitName, out var actionInfluence)) continue;
            if (!actionInfluence.TryGetValue(action, out double coefficient)) continue;

            int traitValue = (int)typeof(Personality).GetProperty(traitName)!.GetValue(personality)!;
            weight += (traitValue / 100.0 - 0.5) * coefficient;
        }
        return weight;
    }
}
