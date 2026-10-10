namespace LivingWorld.Domain.Shared;

/// <summary>Regra de cenário declarada opcionalmente; ausente, cai no default documentado do
/// tipo. Mesmo padrão repetido em <c>AttentionRules</c>/<c>PowerUtilityRules</c>/
/// <c>PowerInheritanceRules</c>/<c>BodyRules</c> — um só lugar pra não duplicar o corpo.</summary>
public static class ScenarioRuleDefaults
{
    public static T Resolve<T>(T? declared, T @default) where T : class => declared ?? @default;
}
