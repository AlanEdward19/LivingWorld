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

    /// <summary>#246 (5º teste, AD-237): sair do personagem devolve a pessoa à vontade do motor, na casa e no trabalho dela, em vez de
    /// fazê-la desaparecer (largar casa e emprego e abrir procura). Dado do cenário; ausente = o sumiço de sempre (AD-196). O sandbox liga:
    /// fechar o jogo e "Continuar" não pode desfazer a vida do casal.</summary>
    public bool? LogoutKeepsLife { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public bool KeepsLifeOnLogout => LogoutKeepsLife ?? false;

    /// <summary>Sono (energia) que uma hora de caminhada gasta além do desgaste de sempre: andar cansa.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? StepSleepPerHour { get; init; }

    /// <summary>Abaixo deste sono o passo fica mais lento, até 1,5× com o sono em 0.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? TiredBelow { get; init; }

    /// <summary>AD-197k: passo solto de um adulto descansado em chão plano (m/s); a mesma conta de corpo, carga e chão o escala.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? WalkMetersPerSecond { get; init; }

    /// <summary>#269: até que distância da soleira (m) a roda do E já oferece "Entrar" na porta (antes só na célula exata, e a 1,5 m ela sumia): chegando por um clique ou pela roda o corpo anda até a soleira e entra.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? DoorReachMeters { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public double DoorReachM => DoorReachMeters ?? 2.4;

    /// <summary>AD-197k: metros de uma célula para o passo solto (a escala do desenho: uma pessoa tem pouco mais de uma célula).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? CellMeters { get; init; }

    /// <summary>AD-197k: até quanto tempo de mundo (ms) um rumo vale sem novo comando (o cliente manda o rumo a cada ~100 ms).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WalkHoldMs { get; init; }

    /// <summary>#248: quanto tempo REAL (ms) o corpo segue o rumo sem notícia do cliente antes de parar (o cliente manda a cada ~100 ms; sumiu, para).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? WalkIdleStopMs { get; init; }

    // --- AD-197u: multidão na célula, correr e pular. Nulos no JSON enquanto valem o padrão (os bytes de antes). ---

    /// <summary>AD-197u: quantas pessoas cabem numa célula de 1,5 m (2,25 m²) antes de ela barrar o passo de mais uma.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? CellCrowdLimit { get; init; }

    /// <summary>AD-197u: quanto a corrida multiplica a velocidade do passo.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RunFactorValue { get; init; }

    /// <summary>AD-197u: teto da corrida (m/s): nem o corpo mais leve passa de uma corrida de fôlego.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RunMaxMetersPerSecond { get; init; }

    /// <summary>AD-197u: quantas vezes o gasto de sono da caminhada (<see cref="StepSleep"/>) a corrida gasta.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? RunSleepFactorValue { get; init; }

    /// <summary>AD-197u: abaixo desta saúde (0–100) o corpo não corre nem pula.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? ExertionMinHealthValue { get; init; }

    /// <summary>AD-197u: abaixo deste sono (energia) o corpo não pula (exausto).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpMinSleepValue { get; init; }

    /// <summary>AD-197u: sono (energia) que um pulo gasta.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpSleepCostValue { get; init; }

    /// <summary>AD-197u: tempo no ar (ms) de um adulto de força média; o corpo (massa muscular) o escala.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? JumpAirMsValue { get; init; }

    /// <summary>AD-197u: acima deste custo de movimento do corpo (passada curta de criança, peso, fraqueza) não se pula.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpMaxBodyCostValue { get; init; }

    /// <summary>AD-197u: abaixo desta capacidade de força (massa muscular relativa à média) não se pula (idoso, frágil).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpMinCapacityValue { get; init; }

    /// <summary>#222: abaixo desta idade (anos) o corpo ainda não pula (criança pequena); o custo do corpo deixou de barrar adulto pesado.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpMinAgeYearsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public double JumpMinAgeYears => JumpMinAgeYearsValue ?? 2.0;

    /// <summary>#222: acima desta idade (anos) o corpo já não pula (fragilidade da velhice), qualquer que seja o resto do corpo.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? JumpMaxAgeYearsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public double JumpMaxAgeYears => JumpMaxAgeYearsValue ?? 88.0;

    [System.Text.Json.Serialization.JsonIgnore] public int CellCrowd => CellCrowdLimit ?? 3;
    [System.Text.Json.Serialization.JsonIgnore] public double RunFactor => RunFactorValue ?? 2.2;
    [System.Text.Json.Serialization.JsonIgnore] public double RunMaxMps => RunMaxMetersPerSecond ?? 4.5;
    [System.Text.Json.Serialization.JsonIgnore] public double RunSleepFactor => RunSleepFactorValue ?? 3.0;
    [System.Text.Json.Serialization.JsonIgnore] public int ExertionMinHealth => ExertionMinHealthValue ?? 30;
    [System.Text.Json.Serialization.JsonIgnore] public double JumpMinSleep => JumpMinSleepValue ?? 15.0;
    [System.Text.Json.Serialization.JsonIgnore] public double JumpSleepCost => JumpSleepCostValue ?? 0.5;
    [System.Text.Json.Serialization.JsonIgnore] public int JumpAirMs => JumpAirMsValue ?? 500;
    [System.Text.Json.Serialization.JsonIgnore] public double JumpMaxBodyCost => JumpMaxBodyCostValue ?? 1.8; // #222: 1,2 recusava adulto alto ou pesado comum
    [System.Text.Json.Serialization.JsonIgnore] public double JumpMinCapacity => JumpMinCapacityValue ?? 0.55;

    [System.Text.Json.Serialization.JsonIgnore] public double WalkMps => WalkMetersPerSecond ?? LivingWorld.Domain.Geography.WorldScale.ReferenceWalkMps;
    [System.Text.Json.Serialization.JsonIgnore] public double CellM => CellMeters ?? LivingWorld.Domain.Geography.WorldScale.CellMeters;
    [System.Text.Json.Serialization.JsonIgnore] public int WalkHold => WalkHoldMs ?? 300;
    /// <summary>#248: o prazo sem comando, em ms de MUNDO (o tempo real do prazo vezes a escala do relógio).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public long WalkIdleStop => (long)Math.Round((WalkIdleStopMs ?? 1000) * Math.Max(1.0, Time.TimeScale));

    [System.Text.Json.Serialization.JsonIgnore] public bool PaceOn => BodyPace ?? true;
    [System.Text.Json.Serialization.JsonIgnore] public double StepSleep => StepSleepPerHour ?? 3.0;
    [System.Text.Json.Serialization.JsonIgnore] public double Tired => TiredBelow ?? 50.0;

    // --- #234: combate (alcance, recarga, esquiva, bloqueio, reação). Nulos no JSON enquanto valem o padrão (os bytes de antes). ---

    /// <summary>Falso desliga o combate com tempo (volta ao golpe imediato do AD-197e): sem alcance em metros, recarga, guarda nem reação.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? CombatTiming { get; init; }

    /// <summary>Alcance do golpe (m) entre o corpo e o alvo: 2,2 m cobre toda célula vizinha de 1,5 m (centro a centro, até 2,12 m na
    /// diagonal) e deixa o passo fino fugir do golpe (escolha do modelo).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? StrikeReachMeters { get; init; }

    /// <summary>Recarga do golpe (ms) de um adulto de força média; o corpo (força) escala.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? StrikeCooldownMsValue { get; init; }

    /// <summary>Janela (ms) da esquiva e recarga até levantar a guarda de novo.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? DodgeWindowMsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? BlockWindowMsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? GuardCooldownMsValue { get; init; }

    /// <summary>Chance base (0–1) de a esquiva evitar o golpe de um corpo de força média e descansado; o corpo escala.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? DodgeChanceValue { get; init; }

    /// <summary>Fração do dano que passa pelo bloqueio (menor = bloqueia mais).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? BlockPassesValue { get; init; }

    /// <summary>Tempo (ms) que quem apanhou leva para revidar, de um impulsivo (0,5×) a um calmo (1,5×).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RetaliateWindupMsValue { get; init; }

    [System.Text.Json.Serialization.JsonIgnore] public bool CombatOn => Enabled && (CombatTiming ?? true);
    [System.Text.Json.Serialization.JsonIgnore] public double StrikeReachM => StrikeReachMeters ?? 2.2;
    [System.Text.Json.Serialization.JsonIgnore] public int StrikeCooldownMs => StrikeCooldownMsValue ?? 900;
    [System.Text.Json.Serialization.JsonIgnore] public int DodgeWindowMs => DodgeWindowMsValue ?? 350;
    [System.Text.Json.Serialization.JsonIgnore] public int BlockWindowMs => BlockWindowMsValue ?? 700;
    [System.Text.Json.Serialization.JsonIgnore] public int GuardCooldownMs => GuardCooldownMsValue ?? 1200;
    [System.Text.Json.Serialization.JsonIgnore] public double DodgeChance => DodgeChanceValue ?? 0.7;
    [System.Text.Json.Serialization.JsonIgnore] public double BlockPasses => BlockPassesValue ?? 0.3;
    [System.Text.Json.Serialization.JsonIgnore] public int RetaliateWindupMs => RetaliateWindupMsValue ?? 600;

    public static readonly PlayerRules Default = new();

    public static readonly PlayerRules Off = new() { Enabled = false };
}
