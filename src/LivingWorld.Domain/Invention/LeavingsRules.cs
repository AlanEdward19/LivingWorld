namespace LivingWorld.Domain.Invention;

/// <summary>O espólio de quem morre longe de casa (AD-168): a carga e o recipiente ficam onde a pessoa morreu, para a casa (ou o
/// herdeiro) buscar numa viagem de verdade ou para quem passa furtar. Números sem fonte firme, escolha do modelo ("precisa de
/// conferência"). <see cref="Enabled"/> falso = o de antes (a carga volta à despensa da casa na hora, esteja o morto onde estiver).</summary>
public sealed record LeavingsRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Idade mínima de quem sai para buscar (a mesma do furto e do trabalho de criança grande).</summary>
    public int MinFetcherAgeYears { get; init; } = 12;

    /// <summary>Até quantas horas de caminho (ida) a casa vai buscar: um dia de jornada. Mais longe, fica para quem passar.</summary>
    public int MaxFetchHours { get; init; } = 24;

    /// <summary>Chance (‰) de quem passa e é de outra casa levar o espólio quando o temperamento passa exatamente da barreira
    /// (inclinação 1, a pessoa comum); escala com a inclinação, até o teto de 1000. Escolha sem fonte.</summary>
    public int TheftChancePm { get; init; } = 250;

    /// <summary>Quanto cada pessoa acordada no lugar (até três) sobe a barreira de quem pensa em levar o que não é seu: o risco de
    /// ser visto. Escolha sem fonte.</summary>
    public double SeenBarrierPerWitness { get; init; } = 0.15;

    /// <summary>AD-182 (08c item 155): quanto o laço de quem passa com o morto (o maior entre confiança e afeto, 0–1) sobe a barreira de
    /// levar o que era dele. Medido com mortes provocadas de viajantes (8 seeds × 8 anos): na vila todo espólio cai a menos de 6 h de
    /// casa e 41% era furtado por vizinhos que conheciam o morto; na aldeia de rosto conhecido o achado de um vizinho morto se
    /// devolve. 0 = o de antes. Escolha do modelo (precisa de conferência).</summary>
    public double KnownDeadBarrier { get; init; } = 0.5;

    /// <summary>Quantas vezes (‰) o estrago no chão é o da despensa (<c>SpoilagePerDayByResource</c>), na temperatura de referência.
    /// AD-182 (08c item 156): 3000 (era 1000, igual à despensa): ao relento a comida pega chuva e orvalho (mofo) e fica ao alcance
    /// de rato, cão e ave, que na despensa fechada não chegam. Sem fonte numérica firme (precisa de conferência).</summary>
    public int GroundSpoilagePm { get; init; } = 3000;

    /// <summary>AD-182: água perdida por dia (‰) no recipiente largado no chão: evapora pela boca e pelo couro ou barro poroso e
    /// vaza (antes a água no chão nunca se perdia). Escolha do modelo, precisa de conferência.</summary>
    public int GroundWaterLossPerDayPm { get; init; } = 20;

    /// <summary>AD-182: quanto a perda ao relento multiplica a cada 10 °C acima de <see cref="GroundReferenceCelsius"/> (Q10 de
    /// mofo, bicho e evaporação, regra geral da biologia de ~2–3). 1 desliga o efeito da temperatura.</summary>
    public double GroundQ10 { get; init; } = 2.0;

    public double GroundReferenceCelsius { get; init; } = 15;

    /// <summary>AD-182: o mês cobra só os dias que o espólio passou no chão (antes, o deixado na véspera perdia o mês inteiro).</summary>
    public bool CountDaysOnGround { get; init; } = true;

    public static LeavingsRules Default { get; } = new();

    public static LeavingsRules Disabled { get; } = new() { Enabled = false };
}
