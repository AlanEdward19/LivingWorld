using LivingWorld.Domain.Behavior;

namespace LivingWorld.Domain.Ontogeny;

/// <summary>Marcos e exigências padrão de um cenário humano. É conteúdo autorado (valores aproximam a literatura de
/// desenvolvimento infantil), não regra de motor: um cenário que declare outro bloco o substitui por inteiro.</summary>
public static class OntogenyDefaults
{
    private static MilestoneDefinition M(string id, DevelopmentAxis axis, double thr, double start, double median, double limit, params string[] requires) =>
        new(id, axis, thr, start, median, limit, requires);

    public static IReadOnlyList<MilestoneDefinition> HumanMilestones { get; } =
    [
        M("hold-head", DevelopmentAxis.GrossMotor, 0.2, 0.0, 0.2, 0.5),
        M("sit", DevelopmentAxis.GrossMotor, 0.4, 0.3, 0.5, 0.8, "hold-head"),
        M("walk", DevelopmentAxis.GrossMotor, 0.6, 0.7, 1.0, 1.5, "sit"),
        M("run", DevelopmentAxis.GrossMotor, 0.85, 1.5, 2.0, 3.0, "walk"),
        M("coordination", DevelopmentAxis.GrossMotor, 1.0, 3.0, 5.0, 8.0, "run"),
        M("grasp", DevelopmentAxis.FineMotor, 0.3, 0.2, 0.4, 0.7),
        M("tools", DevelopmentAxis.FineMotor, 0.7, 1.5, 3.0, 5.0, "grasp"),
        M("skilled-hands", DevelopmentAxis.FineMotor, 1.0, 4.0, 7.0, 12.0, "tools"),
        M("babble", DevelopmentAxis.Language, 0.2, 0.3, 0.5, 0.8),
        M("first-words", DevelopmentAxis.Language, 0.4, 0.7, 1.0, 1.5, "babble"),
        M("sentences", DevelopmentAxis.Language, 0.7, 1.5, 2.5, 4.0, "first-words"),
        M("fluent", DevelopmentAxis.Language, 1.0, 3.0, 6.0, 10.0, "sentences"),
        M("feed-self", DevelopmentAxis.SelfCare, 0.4, 1.0, 2.0, 3.0),
        M("dress-wash", DevelopmentAxis.SelfCare, 0.7, 2.5, 4.0, 6.0, "feed-self"),
        M("independent", DevelopmentAxis.SelfCare, 1.0, 5.0, 9.0, 14.0, "dress-wash"),
        M("social-smile", DevelopmentAxis.SocialCognition, 0.2, 0.1, 0.2, 0.4),
        M("play-together", DevelopmentAxis.SocialCognition, 0.5, 1.5, 3.0, 5.0, "social-smile"),
        M("rules-empathy", DevelopmentAxis.SocialCognition, 0.8, 3.0, 6.0, 10.0, "play-together"),
        M("social-adult", DevelopmentAxis.SocialCognition, 1.0, 8.0, 13.0, 18.0, "rules-empathy"),
        M("symbolic", DevelopmentAxis.Abstraction, 0.3, 2.0, 3.0, 5.0),
        M("reasoning", DevelopmentAxis.Abstraction, 0.7, 6.0, 10.0, 14.0, "symbolic"),
        M("abstract", DevelopmentAxis.Abstraction, 1.0, 11.0, 15.0, 20.0, "reasoning"),
    ];

    /// <summary>O recém-nascido só chora, mama e dorme: o resto exige um marco.</summary>
    public static IReadOnlyDictionary<ActionType, MilestoneRequirement> HumanRequirements { get; } =
        new Dictionary<ActionType, MilestoneRequirement>
        {
            [ActionType.Cry] = new(DevelopmentAxis.GrossMotor, 0),
            [ActionType.Eat] = new(DevelopmentAxis.GrossMotor, 0),
            [ActionType.Sleep] = new(DevelopmentAxis.GrossMotor, 0),
            [ActionType.Idle] = new(DevelopmentAxis.GrossMotor, 0),
            [ActionType.Travel] = new(DevelopmentAxis.GrossMotor, 0.6),
            [ActionType.Socialize] = new(DevelopmentAxis.SocialCognition, 0.5),
            [ActionType.Buy] = new(DevelopmentAxis.Abstraction, 0.7),
            [ActionType.Work] = new(DevelopmentAxis.FineMotor, 1.0),
            [ActionType.UsePower] = new(DevelopmentAxis.Abstraction, 0.7),
            [ActionType.Steal] = new(DevelopmentAxis.SocialCognition, 0.8),
        };

    /// <summary>Regras ligadas, com os marcos humanos padrão.</summary>
    public static OntogenyRules Human { get; } =
        OntogenyRules.Create(true, HumanMilestones, HumanRequirements).Value!;
}
