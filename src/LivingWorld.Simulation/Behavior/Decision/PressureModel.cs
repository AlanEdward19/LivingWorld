using LivingWorld.Domain.Cognition;

namespace LivingWorld.Simulation.Behavior.Decision;

/// <summary>Pressões do rastro cognitivo, agrupando os termos da utilidade da ação vencedora (AD-138). Antes as pressões eram
/// inventadas ao lado da escolha (renda e proteger a casa apareciam sem pesar em nada, achado 11); agora cada pressão é a soma
/// dos termos que de fato entraram na nota, e o que não entrou não aparece.</summary>
public static class PressureModel
{
    public const string AcquireFood = "AcquireFood";
    public const string EarnIncome = "EarnIncome";
    public const string SeekRest = "SeekRest";
    public const string SeekSocial = "SeekSocial";

    /// <summary>Pressão a que um termo pertence: comida e água, descanso, convívio, renda; os demais (vida interior,
    /// personalidade, continuidade) aparecem com o próprio nome.</summary>
    public static string KindOf(UtilityTerm term) => term switch
    {
        UtilityTerm.Hunger or UtilityTerm.Thirst or UtilityTerm.Provision or UtilityTerm.EmptyPantry
            or UtilityTerm.FamineMemory or UtilityTerm.ScarcityBelief or UtilityTerm.Cry => AcquireFood,
        UtilityTerm.Sleep => SeekRest,
        UtilityTerm.Social or UtilityTerm.StrongBond => SeekSocial,
        UtilityTerm.IncomeNeed => EarnIncome,
        _ => term.ToString(),
    };

    /// <summary>Função pura: termos positivos da ação vencedora → pressões (intensidade = soma dos pesos, fatores = nomes dos
    /// termos, do maior para o menor).</summary>
    public static IReadOnlyList<Pressure> FromTerms(IEnumerable<UtilityContribution> winnerTerms)
    {
        ArgumentNullException.ThrowIfNull(winnerTerms);
        return winnerTerms
            .Where(t => t.Value > 0)
            .GroupBy(t => KindOf(t.Term))
            .Select(g => new Pressure(
                g.Key,
                g.Sum(t => t.Value),
                g.OrderByDescending(t => t.Value).ThenBy(t => t.Term).Select(t => t.Term.ToString()).ToList()))
            .OrderBy(p => p.Kind, StringComparer.Ordinal)
            .ToList();
    }
}
