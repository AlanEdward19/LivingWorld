using System.Text.Json.Serialization;
using LivingWorld.Domain.Population.Skills;

namespace LivingWorld.Domain.Population.Family;

/// <summary>Os 4 eixos (Confiança, Afeto, Respeito, Dívida) de A→B — nunca o mesmo objeto usado
/// para B→A. Classe mutável (não <c>record</c>): os eixos mudam com
/// frequência alta (<c>Daily</c>) para até milhares de pares — um <c>record with</c> a cada dia
/// seria alocação desnecessária (mesmo raciocínio de <see cref="SkillSet"/> vs
/// <see cref="Personality"/>, Tech Decision do design).</summary>
public sealed class Relationship
{
    private const double AxisMin = 0.0;
    private const double AxisMax = 100.0;

    /// <summary>Carimbo do dono (<c>From</c>) desta relação, compartilhado por todas as relações dele e ligado pelo mundo
    /// que a guarda. Toda mudança de eixo o avança; serve só para invalidar caches derivados do próprio dono
    /// (<c>"tem vínculo forte?"</c>). Era um contador estático do processo: a compra de um NPC invalidava a resposta
    /// de todos os outros, e de todos os outros mundos do processo (#153).</summary>
    [JsonIgnore]
    public RelationshipAxesStamp? OwnerStamp { get; set; }

    private void TouchAxes() => OwnerStamp?.Touch();

    /// <summary>Posição desta relação na ordem de criação que o mundo mantém (derivada, nunca persistida).</summary>
    [JsonIgnore]
    public int OrderSlot { get; set; }

    public double Trust { get; private set; }
    public double Affection { get; private set; }
    public double Respect { get; private set; }
    public double Debt { get; private set; }

    public long LastContactTick { get; private set; }

    /// <summary>Não-pública, mas anotada para <c>System.Text.Json</c> reidratar o snapshot
    /// — nenhum construtor público é exposto de propósito, todo caminho de produção
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

    /// <summary>Cópia com eixos explícitos (instanciação clone/split) — clamp
    /// <c>[0,100]</c>, mesmo piso/teto de <see cref="Set"/>.</summary>
    public static Relationship FromAxes(
        double trust, double affection, double respect, double debt, long firstContactTick) =>
        new(
            Math.Clamp(trust, AxisMin, AxisMax),
            Math.Clamp(affection, AxisMin, AxisMax),
            Math.Clamp(respect, AxisMin, AxisMax),
            Math.Clamp(debt, AxisMin, AxisMax),
            firstContactTick);

    /// <summary>Eixos em array fixo: <c>Enum.GetValues&lt;T&gt;()</c> aloca um array novo a cada chamada, e isto roda por par e por compra.</summary>
    private static readonly RelationshipAxis[] Axes = Enum.GetValues<RelationshipAxis>();

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
        TouchAxes();
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

    /// <summary>Aplica o delta declarado do evento a cada eixo que ele afeta, clamped a
    /// <c>[0,100]</c>. Eixos sem delta declarado para este evento (delta 0) ficam inalterados.</summary>
    public void ApplyEvent(RelationshipEventType type, FamilyRules rules)
    {
        foreach (var axis in Axes)
        {
            double delta = rules.RelationshipEventDelta(type, axis);
            if (delta != 0)
                Set(axis, Get(axis) + delta);
        }
    }

    /// <summary>Decai todos os eixos em direção a <see cref="FamilyRules.NeutralAxisValue"/> por
    /// <see cref="FamilyRules.DecayPerDay"/> — nunca ultrapassa o neutro (não oscila em
    /// torno dele): um eixo acima do neutro nunca cai abaixo dele num único decaimento, e
    /// vice-versa.</summary>
    public void DecayTowardNeutral(FamilyRules rules)
    {
        foreach (var axis in Axes)
        {
            double current = Get(axis);
            double neutral = rules.NeutralAxisValue;

            if (current > neutral)
                Set(axis, Math.Max(current - rules.DecayPerDay, neutral));
            else if (current < neutral)
                Set(axis, Math.Min(current + rules.DecayPerDay, neutral));
        }
    }

    /// <summary>Convivência com rendimento decrescente (AD-128): confiança e afeto sobem cada vez menos conforme chegam ao
    /// teto do par e nunca o passam; quem já está acima do teto (vínculo herdado de outro tempo) não perde por conviver. Os
    /// outros eixos e os deltas negativos seguem lineares, como em <see cref="ApplyEvent"/>.</summary>
    public void ApplyBondingEvent(RelationshipEventType type, FamilyRules rules, double trustCeiling, double affectionCeiling)
    {
        foreach (var axis in Axes)
        {
            double delta = rules.RelationshipEventDelta(type, axis);
            if (delta == 0) continue;

            double current = Get(axis);
            if (delta > 0 && axis is RelationshipAxis.Trust or RelationshipAxis.Affection)
            {
                double ceiling = axis == RelationshipAxis.Trust ? trustCeiling : affectionCeiling;
                if (ceiling <= 0 || current >= ceiling) continue;
                delta = Math.Min(delta * (1.0 - current / ceiling), ceiling - current);
            }

            Set(axis, current + delta);
        }
    }

    /// <summary>Esfriamento proporcional de <paramref name="days"/> dias sem contato (AD-128): cada eixo perde a mesma fração
    /// da distância até o neutro, então vínculo forte dura anos e conhecido de vista some em meses. Confiança e afeto que
    /// estão no piso de parentesco ou acima dele não descem abaixo dele (<paramref name="kinFloor"/> 0 = sem piso).</summary>
    public void Fade(FamilyRules rules, int days, double kinFloor)
    {
        // potência inteira por multiplicação: o mundo não usa a matemática transcendental da plataforma
        double daily = Math.Max(0.0, 1.0 - rules.DecayPerDay / 100.0), keep = 1.0;
        for (int i = 0; i < days; i++) keep *= daily;
        double neutral = rules.NeutralAxisValue;
        foreach (var axis in Axes)
        {
            double current = Get(axis);
            if (current == neutral) continue;

            double faded = neutral + (current - neutral) * keep;
            if (kinFloor > 0 && axis is RelationshipAxis.Trust or RelationshipAxis.Affection && current >= kinFloor)
                faded = Math.Max(faded, kinFloor);
            if (faded != current) Set(axis, faded);
        }
    }

    /// <summary>Todos os eixos a menos de <see cref="SocialBondRules.ForgottenBelow"/> do neutro: não sobrou nada do vínculo.</summary>
    public bool IsFaded(double neutral) =>
        Math.Abs(Trust - neutral) < SocialBondRules.ForgottenBelow && Math.Abs(Affection - neutral) < SocialBondRules.ForgottenBelow
        && Math.Abs(Respect - neutral) < SocialBondRules.ForgottenBelow && Math.Abs(Debt - neutral) < SocialBondRules.ForgottenBelow;

    public void MarkContact(long tick) => LastContactTick = tick;

    /// <summary>Copia os 4 eixos de <paramref name="source"/> (transferência de
    /// vínculos em clone/split) — não altera <see cref="LastContactTick"/> (o chamador marca
    /// contato no tick atual).</summary>
    public void CopyAxesFrom(Relationship source)
    {
        TouchAxes();
        Trust = source.Trust;
        Affection = source.Affection;
        Respect = source.Respect;
        Debt = source.Debt;
    }
}

/// <summary>Versão monotônica das relações de um dono: avança em toda criação, remoção ou mudança de eixo de uma relação
/// dele. Derivada, nunca persistida. O tick é serial por mundo, então o incremento não precisa ser atômico.</summary>
public sealed class RelationshipAxesStamp
{
    public long Version { get; private set; }

    public void Touch() => Version++;
}
