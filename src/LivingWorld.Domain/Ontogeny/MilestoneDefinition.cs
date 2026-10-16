namespace LivingWorld.Domain.Ontogeny;

/// <summary>Marco de desenvolvimento declarado no cenário: ao atingir <see cref="Threshold"/> no eixo o NPC "tem" o
/// marco. A janela (anos) diz quando ele costuma chegar: início, mediana e limite; <see cref="Requires"/> lista os
/// marcos que precisam existir antes (ids, não código).</summary>
public sealed record MilestoneDefinition(
    string Id,
    DevelopmentAxis Axis,
    double Threshold,
    double WindowStartYears,
    double WindowMedianYears,
    double WindowLimitYears,
    IReadOnlyList<string> Requires);

/// <summary>Pré-requisito de uma ação: o eixo precisa ter chegado a <see cref="Threshold"/> (0 = sempre elegível).</summary>
public sealed record MilestoneRequirement(DevelopmentAxis Axis, double Threshold);
