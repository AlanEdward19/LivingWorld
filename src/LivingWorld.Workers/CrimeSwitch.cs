using LivingWorld.Domain.Crime;

namespace LivingWorld.Workers;

/// <summary>Medição do crime contra a pessoa (AD-154), só para os relatórios: <c>LW_CRIME_OFF</c> com vírgulas tira a chave
/// de cada crime (<c>domestic</c>, <c>kidnap</c>, <c>sexual</c>) para isolar o efeito de cada um. Vazio = padrão.</summary>
public static class CrimeSwitch
{
    public static CrimeRules? CrimeRules()
    {
        var keys = (Environment.GetEnvironmentVariable("LW_CRIME_OFF") ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (keys.Count == 0) return null;
        var rules = Domain.Crime.CrimeRules.Default;
        if (keys.Contains("domestic")) rules = rules with { Domestic = null };
        if (keys.Contains("kidnap")) rules = rules with { Kidnapping = null };
        if (keys.Contains("sexual")) rules = rules with { SexualViolence = null };
        return rules;
    }
}
