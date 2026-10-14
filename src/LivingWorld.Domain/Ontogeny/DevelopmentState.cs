namespace LivingWorld.Domain.Ontogeny;

/// <summary>Progresso do NPC em cada eixo, em [0,1]. <c>null</c> em <c>Npc.Development</c> = desenvolvimento completo
/// (sistema inerte, custo zero e nenhum byte no snapshot).</summary>
public sealed record DevelopmentState(
    double GrossMotor, double FineMotor, double Language, double SelfCare, double SocialCognition, double Abstraction)
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
}
