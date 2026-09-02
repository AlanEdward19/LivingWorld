using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;

namespace LivingWorld.Tests.Unit.Behavior;

/// <summary>Fase 4, task 5: <see cref="PersonalityWeighting.WeightOf"/> — peso = 1 +
/// (traço/100 − 0.5) × influência[traço][ação]. Cobertura por reflexão garante que todo
/// traço de <see cref="Personality"/> tem ao menos uma entrada na tabela de influência
/// (NEEDS-08: reprova se um traço não tiver linha). A tabela de casos abaixo (traço,
/// açãoBaixo=20, açãoAlto=80) é a mesma usada de verdade na Task 13 (utility AI real).</summary>
public class PersonalityWeightingTests
{
    /// <summary>[Traço, Ação modulada, direção positiva?] — uma linha por traço declarado em
    /// <see cref="Personality"/>; <c>Impulsivity</c> tem duas (Idle positivo, Work
    /// negativo).</summary>
    public static readonly TheoryData<string, ActionType, bool> TraitDirectionCases = new()
    {
        { nameof(Personality.Conscientiousness), ActionType.Work, true },
        { nameof(Personality.Ambition), ActionType.Work, true },
        { nameof(Personality.Extroversion), ActionType.Socialize, true },
        { nameof(Personality.Openness), ActionType.Socialize, true },
        { nameof(Personality.Altruism), ActionType.Socialize, true },
        { nameof(Personality.Loyalty), ActionType.Work, true },
        { nameof(Personality.Impulsivity), ActionType.Idle, true },
        { nameof(Personality.Impulsivity), ActionType.Work, false },
        { nameof(Personality.RiskAversion), ActionType.Travel, false },
        { nameof(Personality.EmotionalStability), ActionType.Idle, false },
        { nameof(Personality.Agreeableness), ActionType.Socialize, true },
    };

    private static Personality WithTrait(string traitName, int value)
    {
        var props = new Dictionary<string, int>
        {
            [nameof(Personality.Extroversion)] = 50,
            [nameof(Personality.Agreeableness)] = 50,
            [nameof(Personality.Conscientiousness)] = 50,
            [nameof(Personality.EmotionalStability)] = 50,
            [nameof(Personality.Openness)] = 50,
            [nameof(Personality.Ambition)] = 50,
            [nameof(Personality.Loyalty)] = 50,
            [nameof(Personality.Altruism)] = 50,
            [nameof(Personality.Impulsivity)] = 50,
            [nameof(Personality.RiskAversion)] = 50,
        };
        props[traitName] = value;

        return Personality.Create(
            props[nameof(Personality.Extroversion)], props[nameof(Personality.Agreeableness)],
            props[nameof(Personality.Conscientiousness)], props[nameof(Personality.EmotionalStability)],
            props[nameof(Personality.Openness)], props[nameof(Personality.Ambition)],
            props[nameof(Personality.Loyalty)], props[nameof(Personality.Altruism)],
            props[nameof(Personality.Impulsivity)], props[nameof(Personality.RiskAversion)]).Value!;
    }

    [Fact]
    public void Every_personality_trait_has_at_least_one_influence_table_entry()
    {
        Assert.Equal(10, PersonalityWeighting.AllTraitNames.Count);
        foreach (var trait in PersonalityWeighting.AllTraitNames)
            Assert.True(PersonalityWeighting.HasInfluenceEntry(trait), $"trait '{trait}' sem entrada na tabela de influência");
    }

    [Theory]
    [MemberData(nameof(TraitDirectionCases))]
    public void WeightOf_moves_in_the_documented_direction_between_trait_20_and_80(string trait, ActionType action, bool positiveDirection)
    {
        var low = WithTrait(trait, 20);
        var high = WithTrait(trait, 80);

        double weightLow = PersonalityWeighting.WeightOf(low, action);
        double weightHigh = PersonalityWeighting.WeightOf(high, action);

        if (positiveDirection)
            Assert.True(weightHigh > weightLow, $"esperava peso({action}) crescente com {trait} alto");
        else
            Assert.True(weightHigh < weightLow, $"esperava peso({action}) decrescente com {trait} alto");
    }

    [Fact]
    public void WeightOf_returns_baseline_one_when_trait_is_at_midpoint_50()
    {
        var neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

        Assert.Equal(1.0, PersonalityWeighting.WeightOf(neutral, ActionType.Work));
        Assert.Equal(1.0, PersonalityWeighting.WeightOf(neutral, ActionType.Socialize));
        Assert.Equal(1.0, PersonalityWeighting.WeightOf(neutral, ActionType.Idle));
        Assert.Equal(1.0, PersonalityWeighting.WeightOf(neutral, ActionType.Travel));
    }

    [Fact]
    public void WeightOf_is_unaffected_by_traits_that_do_not_influence_the_action()
    {
        var neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
        var eatIrrelevant = WithTrait(nameof(Personality.Conscientiousness), 100);

        Assert.Equal(PersonalityWeighting.WeightOf(neutral, ActionType.Eat), PersonalityWeighting.WeightOf(eatIrrelevant, ActionType.Eat));
    }
}
