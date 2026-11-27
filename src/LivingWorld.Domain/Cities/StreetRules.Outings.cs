namespace LivingWorld.Domain.Cities;

/// <summary>#242: depois do turno (e no tempo livre de quem não trabalha) a pessoa decide pelo estado dela aonde ir: casa, largo, mercado,
/// casa de quem conhece ou um passeio curto, e a viagem é de verdade (metros reais), em vez de ficar à toa onde o turno acabou.
/// Desligada (<c>LW_AD192=nooutings</c>) vale o passeio de um passo a cada 3 h de antes, para o pareado.</summary>
public sealed partial record StreetRules
{
    public bool AfterWorkOutings { get; init; } = true;

    /// <summary>Primeira e última hora (inclusive) em que a pessoa sai; fora delas fica onde está (a noite é de casa).</summary>
    public int OutingFromHour { get; init; } = 6;
    public int OutingUntilHour { get; init; } = 20;

    /// <summary>Horas que uma escolha dura (a mesma sorte vale o bloco todo, então quem chegou fica).</summary>
    public int OutingBlockHours { get; init; } = 6;

    /// <summary>Fração das vezes em que quem quer conversar e já tem gente por perto fica onde está (a conversa não vira viagem).</summary>
    public double OutingStayWithCompany { get; init; } = 0.7;

    /// <summary>O passeio curto: de tantas a tantas células (de 1,5 m) da casa.</summary>
    public int OutingStrollMinCells { get; init; } = 3;
    public int OutingStrollMaxCells { get; init; } = 8;

    /// <summary>A partir desta hora a casa pesa mais na escolha (o fim do dia).</summary>
    public int OutingHomewardHour { get; init; } = 19;
}
