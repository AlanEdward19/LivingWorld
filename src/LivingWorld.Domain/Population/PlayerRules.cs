using LivingWorld.Domain.Cognition;

namespace LivingWorld.Domain.Population;

/// <summary>O que acontece com quem sumiu e nunca mais foi achado (AD-196, Fase 25): dado de cenário, não regra escondida.</summary>
public enum AbandonmentPolicy
{
    /// <summary>Segue desaparecido: vive pelas regras de sempre até morrer ou ser achado.</summary>
    StayMissing = 0,

    /// <summary>Declarado morto depois de <see cref="PlayerRules.DeclareDeadAfterDays"/> dias: herança, dívida, viuvez e luto como
    /// qualquer morte.</summary>
    DeclareDead = 1,

    /// <summary>Volta ao pool: depois de <see cref="PlayerRules.ReturnToPoolAfterDays"/> dias o motor o traz de volta para casa,
    /// como qualquer NPC (a volta passa pela mesma reação do retorno tardio).</summary>
    ReturnToPool = 2,
}

/// <summary>Regras do desaparecimento e da volta (AD-196). Nulo no mundo = <see cref="Default"/>, omitido do hash: o mundo sem
/// jogador não muda. <see cref="Enabled"/> falso (<c>LW_AD196=off</c>) volta ao AD-111: vínculo de parente sempre 100, procura de
/// 30 dias para todos, sem luto na psique nem reação à volta, e a encarnação registrada no log do mundo.</summary>
public sealed record PlayerRules
{
    public bool Enabled { get; init; } = true;

    public AbandonmentPolicy Abandonment { get; init; } = AbandonmentPolicy.StayMissing;

    /// <summary>Dias até a declaração de morte presumida. Sete anos: a presunção de morte do direito comum e do canônico
    /// medieval (escolha do modelo; o cenário troca).</summary>
    public int DeclareDeadAfterDays { get; init; } = 7 * 365;

    /// <summary>Dias até o motor reassumir quem sumiu (política <see cref="AbandonmentPolicy.ReturnToPool"/>): um ano.</summary>
    public int ReturnToPoolAfterDays { get; init; } = 365;

    /// <summary>O declarado morto pode voltar? Falso (o padrão conservador, decisão pendente do dono): a declaração é a morte do
    /// personagem. Verdadeiro: herança, viuvez e luto correm, mas ele segue vivo e desaparecido e pode voltar.</summary>
    public bool PresumedDeadMayReturn { get; init; }

    /// <summary>Dias máximos de procura de um vínculo de força 100; cada um procura na proporção do vínculo (mínimo 1 dia).</summary>
    public int SearchDaysAtFullBond { get; init; } = 30;

    /// <summary>O que o sumiço de alguém de força 100 deixa em quem fica (lembrança e emoção na escala do vínculo): menos que a
    /// morte de um cônjuge (não há corpo, há esperança), com medo pelo que pode ter acontecido.</summary>
    public LifeEventImpact KinDisappeared { get; init; } = new(70, Grief: 0.6, Fear: 0.4);

    /// <summary>O reencontro com alguém de força 100 (alegria), mais marcante quanto mais tempo sumido.</summary>
    public LifeEventImpact Reunited { get; init; } = new(60, Joy: 0.8);

    /// <summary>Dias sumido em que o reencontro chega à saliência cheia (cresce em linha até lá a partir de 60%).</summary>
    public int ReunionFullSalienceDays { get; init; } = 365;

    /// <summary>AD-197e: cuidado (0–100: vínculo × lealdade, ver <c>PossessionWill</c>) a partir do qual a pessoa em posse comum
    /// recusa ferir alguém. 60 = parente ou amigo de vínculo forte de alguém de lealdade média (escolha do modelo).</summary>
    public double RefuseHarmCare { get; init; } = 60;

    /// <summary>AD-197e: o que a posse plena deixa em quem foi possuído, na escala do sofrimento (0–100 → 0–1) com piso de 0,3: ter
    /// o corpo tomado já marca, e ver o corpo ferir quem se ama marca mais.</summary>
    public LifeEventImpact Possessed { get; init; } = new(75, Fear: 0.7, Anger: 0.5, Grief: 0.3, Shame: 0.3);

    /// <summary>AD-197f: as regras da posse comum como disputa de vontade, do estranhamento e da reação. Nulo = o padrão.</summary>
    public PossessionRules Possession { get; init; } = PossessionRules.Default;

    /// <summary>AD-197c: ritmo do relógio com e sem jogador, passo fino e sono acelerado. Nulo = o padrão.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeRules Time { get; init; } = TimeRules.Default;

    /// <summary>A forma no JSON de <see cref="Time"/>: fora quando é o padrão, para que o mundo com regras de jogador declaradas tenha
    /// os bytes (e o hash) de antes da AD-197c.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("Time")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TimeRules? TimeOrDefault
    {
        get => Time == TimeRules.Default ? null : Time;
        init => Time = value ?? TimeRules.Default;
    }

    // --- AD-197j: o passo do corpo conduzido. Nulos no JSON enquanto valem o padrão (os bytes de antes). ---

    /// <summary>Falso desliga a cadência do passo pelo corpo (o passo vale na hora, como na AD-197d).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? BodyPace { get; init; }

    /// <summary>Sono (energia) que uma hora de caminhada gasta além do desgaste de sempre: andar cansa.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? StepSleepPerHour { get; init; }

    /// <summary>Abaixo deste sono o passo fica mais lento, até 1,5× com o sono em 0.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? TiredBelow { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public bool PaceOn => BodyPace ?? true;
    [System.Text.Json.Serialization.JsonIgnore] public double StepSleep => StepSleepPerHour ?? 3.0;
    [System.Text.Json.Serialization.JsonIgnore] public double Tired => TiredBelow ?? 50.0;

    public static readonly PlayerRules Default = new();

    public static readonly PlayerRules Off = new() { Enabled = false };
}
