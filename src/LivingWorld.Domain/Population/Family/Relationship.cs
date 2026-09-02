using System.Text.Json.Serialization;

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

    /// <summary>Não-pública, mas anotada para <c>System.Text.Json</c> reidratar o snapshot
    /// (Fase 7, T8) — nenhum construtor público é exposto de propósito, todo caminho de produção
    /// passa por <see cref="Initial"/> (o encontro nunca começa em valor alto).</summary>
    [JsonConstructor]
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

    /// <summary>Cópia com eixos explícitos (instanciação clone/split, REALISM-29) — clamp
    /// <c>[0,100]</c>, mesmo piso/teto de <see cref="Set"/>.</summary>
    public static Relationship FromAxes(
        double trust, double affection, double respect, double debt, long firstContactTick) =>
        new(
            Math.Clamp(trust, AxisMin, AxisMax),
            Math.Clamp(affection, AxisMin, AxisMax),
            Math.Clamp(respect, AxisMin, AxisMax),
            Math.Clamp(debt, AxisMin, AxisMax),
            firstContactTick);

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

    /// <summary>Copia os 4 eixos de <paramref name="source"/> (REALISM-29: transferência de
    /// vínculos em clone/split) — não altera <see cref="LastContactTick"/> (o chamador marca
    /// contato no tick atual).</summary>
    public void CopyAxesFrom(Relationship source)
    {
        Trust = source.Trust;
        Affection = source.Affection;
        Respect = source.Respect;
        Debt = source.Debt;
    }
}
