namespace LivingWorld.Domain.Ontogeny;

/// <summary>Progresso do NPC em cada eixo e o teto que ainda pode alcançar, ambos em [0,1]. O teto cai de forma permanente quando uma
/// janela de marco fecha sem o marco (21b). <c>Fluency</c> reparte o progresso de linguagem entre os idiomas de quem cria (21c); <c>null</c> = nenhum ainda. <c>null</c> em <c>Npc.Development</c> = desenvolvimento completo (sistema inerte).</summary>
public sealed record DevelopmentState(
    double GrossMotor, double FineMotor, double Language, double SelfCare, double SocialCognition, double Abstraction,
    double GrossMotorCeiling = 1.0, double FineMotorCeiling = 1.0, double LanguageCeiling = 1.0,
    double SelfCareCeiling = 1.0, double SocialCognitionCeiling = 1.0, double AbstractionCeiling = 1.0,
    LanguageFluency? Fluency = null)
{
    public static DevelopmentState Newborn { get; } = new(0, 0, 0, 0, 0, 0);

    public double Get(DevelopmentAxis axis) => axis switch
    {
        DevelopmentAxis.GrossMotor => GrossMotor,
        DevelopmentAxis.FineMotor => FineMotor,
        DevelopmentAxis.Language => Language,
        DevelopmentAxis.SelfCare => SelfCare,
        DevelopmentAxis.SocialCognition => SocialCognition,
        DevelopmentAxis.Abstraction => Abstraction,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "eixo desconhecido"),
    };

    public double CeilingOf(DevelopmentAxis axis) => axis switch
    {
        DevelopmentAxis.GrossMotor => GrossMotorCeiling,
        DevelopmentAxis.FineMotor => FineMotorCeiling,
        DevelopmentAxis.Language => LanguageCeiling,
        DevelopmentAxis.SelfCare => SelfCareCeiling,
        DevelopmentAxis.SocialCognition => SocialCognitionCeiling,
        DevelopmentAxis.Abstraction => AbstractionCeiling,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "eixo desconhecido"),
    };

    public DevelopmentState With(DevelopmentAxis axis, double value) => axis switch
    {
        DevelopmentAxis.GrossMotor => this with { GrossMotor = value },
        DevelopmentAxis.FineMotor => this with { FineMotor = value },
        DevelopmentAxis.Language => this with { Language = value },
        DevelopmentAxis.SelfCare => this with { SelfCare = value },
        DevelopmentAxis.SocialCognition => this with { SocialCognition = value },
        DevelopmentAxis.Abstraction => this with { Abstraction = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "eixo desconhecido"),
    };

    public DevelopmentState WithCeiling(DevelopmentAxis axis, double value) => axis switch
    {
        DevelopmentAxis.GrossMotor => this with { GrossMotorCeiling = value },
        DevelopmentAxis.FineMotor => this with { FineMotorCeiling = value },
        DevelopmentAxis.Language => this with { LanguageCeiling = value },
        DevelopmentAxis.SelfCare => this with { SelfCareCeiling = value },
        DevelopmentAxis.SocialCognition => this with { SocialCognitionCeiling = value },
        DevelopmentAxis.Abstraction => this with { AbstractionCeiling = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "eixo desconhecido"),
    };

    /// <summary>Todo eixo no máximo e sem teto reduzido: o NPC não tem mais nada a adquirir (vira <c>null</c> no <c>Npc</c>).</summary>
    public bool IsComplete =>
        GrossMotor >= Full && FineMotor >= Full && Language >= Full && SelfCare >= Full && SocialCognition >= Full && Abstraction >= Full;

    /// <summary>Tolerância de ponto flutuante: a soma de ganhos mensais chega a 0,9999999999999999, que é 1 para todo efeito.</summary>
    private const double Full = 1.0 - 1e-9;
}
