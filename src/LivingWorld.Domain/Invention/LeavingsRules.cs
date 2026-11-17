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

    /// <summary>Quantas vezes (‰) o estrago no chão é o da despensa (<c>SpoilagePerDayByResource</c>): 1000 = igual. Escolha
    /// conservadora (no chão, ao tempo e aos bichos, deveria ser mais; precisa de conferência).</summary>
    public int GroundSpoilagePm { get; init; } = 1000;

    public static LeavingsRules Default { get; } = new();

    public static LeavingsRules Disabled { get; } = new() { Enabled = false };
}
