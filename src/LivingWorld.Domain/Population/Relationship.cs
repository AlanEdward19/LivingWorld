namespace LivingWorld.Domain;

/// <summary>Os 4 eixos (Confiança, Afeto, Respeito, Dívida) de A→B — nunca o mesmo objeto usado
/// para B→A (Fase 7, T5, FAM-01). Classe mutável (não <c>record</c>): os eixos mudam com
/// frequência alta (<c>Daily</c>) para até milhares de pares — um <c>record with</c> a cada dia
/// seria alocação desnecessária (mesmo raciocínio de <see cref="SkillSet"/> vs
/// <see cref="Personality"/>, Tech Decision do design).</summary>
public sealed class Relationship
{
    private const double AxisMin = 0.0;
    private const double AxisMax = 100.0;

    public double Trust { get; private set; }
    public double Affection { get; private set; }
    public double Respect { get; private set; }
    public double Debt { get; private set; }

    public long LastContactTick { get; private set; }

    private Relationship(double trust, double affection, double respect, double debt, long lastContactTick)
    {
        Trust = trust;
        Affection = affection;
        Respect = respect;
        Debt = debt;
        LastContactTick = lastContactTick;
    }

    /// <summary>Relação recém-criada por um primeiro encontro — todos os eixos no piso mínimo da
    /// escala (<c>0</c>), nunca salta para valor alto num único encontro (Edge Case da spec). A
    /// evolução real vem do primeiro <see cref="ApplyEvent"/> que o chamador aplicar em seguida.</summary>
    public static Relationship Initial(long firstContactTick) => new(AxisMin, AxisMin, AxisMin, AxisMin, firstContactTick);

    public double Get(RelationshipAxis axis) => axis switch
    {
        RelationshipAxis.Trust => Trust,
        RelationshipAxis.Affection => Affection,
        RelationshipAxis.Respect => Respect,
        RelationshipAxis.Debt => Debt,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "RelationshipAxis sem acesso direto declarado"),
    };

    private void Set(RelationshipAxis axis, double value)
    {
        double clamped = Math.Clamp(value, AxisMin, AxisMax);
        switch (axis)
        {
            case RelationshipAxis.Trust: Trust = clamped; break;
            case RelationshipAxis.Affection: Affection = clamped; break;
            case RelationshipAxis.Respect: Respect = clamped; break;
            case RelationshipAxis.Debt: Debt = clamped; break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, "RelationshipAxis sem acesso direto declarado");
        }
    }

    /// <summary>Aplica o delta declarado do evento a cada eixo que ele afeta (FAM-03), clamped a
    /// <c>[0,100]</c>. Eixos sem delta declarado para este evento (delta 0) ficam inalterados.</summary>
    public void ApplyEvent(RelationshipEventType type, FamilyRules rules)
    {
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
        {
            double delta = rules.RelationshipEventDelta(type, axis);
            if (delta != 0)
                Set(axis, Get(axis) + delta);
        }
    }

    /// <summary>Decai todos os eixos em direção a <see cref="FamilyRules.NeutralAxisValue"/> por
    /// <see cref="FamilyRules.DecayPerDay"/> (FAM-04) — nunca ultrapassa o neutro (não oscila em
    /// torno dele): um eixo acima do neutro nunca cai abaixo dele num único decaimento, e
    /// vice-versa.</summary>
    public void DecayTowardNeutral(FamilyRules rules)
    {
        foreach (var axis in Enum.GetValues<RelationshipAxis>())
        {
            double current = Get(axis);
            double neutral = rules.NeutralAxisValue;

            if (current > neutral)
                Set(axis, Math.Max(current - rules.DecayPerDay, neutral));
            else if (current < neutral)
                Set(axis, Math.Min(current + rules.DecayPerDay, neutral));
        }
    }

    public void MarkContact(long tick) => LastContactTick = tick;
}
