using LivingWorld.Domain.Crime;

namespace LivingWorld.Workers;

/// <summary>Medição do crime contra a pessoa (AD-154), só para os relatórios: <c>LW_CRIME_OFF</c> com vírgulas tira a chave
/// de cada crime (<c>domestic</c>, <c>kidnap</c>, <c>sexual</c>) para isolar o efeito de cada um; <c>flog</c> tira o açoite,
/// <c>banish</c> o banimento do ladrão reincidente e <c>domestic=x</c> multiplica a chance da doméstica (AD-163). Vazio = padrão.</summary>
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
        // AD-163 (08b item 80): sem o castigo no corpo (açoite) e sem banir o ladrão grave reincidente
        if (keys.Contains("flog")) rules = rules with { Justice = rules.Justice with { CorporalHarm = 0 } };
        if (keys.Contains("banish")) rules = rules with { Justice = rules.Justice with { BanishAfterConvictions = int.MaxValue } };
        if (keys.Contains("necessity")) rules = rules with { Justice = rules.Justice with { HungerTheftByNecessity = false } }; // AD-190
        // AD-163 (correções #12): fator na chance anual da violência doméstica, `domestic=x`
        if (keys.FirstOrDefault(k => k.StartsWith("domestic=", StringComparison.Ordinal)) is { } dv && rules.Domestic is { } d)
            rules = rules with { Domestic = d with { PerYear = d.PerYear * double.Parse(dv["domestic=".Length..], System.Globalization.CultureInfo.InvariantCulture) } };
        return rules;
    }
}
