namespace LivingWorld.Domain;

/// <summary>Catálogo fechado de ações candidatas da Fase 4 (task 2) — não é conteúdo de
/// cenário: é o próprio modelo de decisão, por isso é <c>enum</c> com valor estável. O valor
/// inteiro <b>é</b> o <c>ActionId</c> usado no desempate de utility AI (NEEDS-06): menor id
/// vence o empate exato.</summary>
public enum ActionType
{
    Eat = 0,
    Sleep = 1,
    Work = 2,
    Socialize = 3,
    Travel = 4,
    Idle = 5,

    /// <summary>Fase 5 (AD-040): viagem a um <c>Workplace</c> de mercado + execução de uma
    /// transação de compra.</summary>
    Buy = 6,
}
