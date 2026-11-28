using System.Text.Json.Serialization;

namespace LivingWorld.Domain.Objects;

/// <summary>#259 (S3 do sandbox): as interações sociais como dado do cenário (<c>scenarios/catalog/social.json</c>). O motor só tem mecanismos genéricos
/// (requisito, aceitação por termos pesados, efeitos, duração, pose); nome, categoria, números e frases são do catálogo. Nulo = cenário sem vida social
/// (omitido do snapshot e do hash).</summary>
public sealed record SocialCatalog(
    SocialRules Rules,
    IReadOnlyList<SocialCategory> Categories,
    IReadOnlyList<SocialInteractionDef> Interactions,
    IReadOnlyList<RelationStateDef> States,
    MoodDef Mood)
{
    public SocialInteractionDef? Find(string id)
    {
        for (int i = 0; i < Interactions.Count; i++)
            if (Interactions[i].Id == id) return Interactions[i];
        return null;
    }

    public SocialCategory? CategoryOf(string id)
    {
        for (int i = 0; i < Categories.Count; i++)
            if (Categories[i].Id == id) return Categories[i];
        return null;
    }
}

/// <summary>Regras gerais: idade adulta, alcance para começar (metros), a pausa entre uma iniciativa e outra de quem ninguém conduz, e a necessidade
/// que pede companhia (<c>InitiateNeed</c>, abaixo de <c>InitiateBelow</c> a pessoa procura alguém), com a sondagem a cada <c>CheckEveryMin</c> minutos de mundo.
/// <c>Texts</c> são as frases dos motivos gerais (<c>actorBusy</c>, <c>targetBusy</c>, <c>targetAsleep</c>, <c>adults</c>, <c>state</c>, <c>affection</c>, <c>trust</c>,
/// <c>noPlace</c>, <c>far</c>), com <c>{actor}</c> e <c>{target}</c>. <c>SpouseAffection</c>/<c>SpouseTrust</c>: o que um casal (sem laço registrado ainda) sente um pelo outro desde o começo.</summary>
public sealed record SocialRules(
    int AdultYears, double ReachM, int CooldownMin, string InitiateNeed, double InitiateBelow, int CheckEveryMin, int HistoryHours,
    IReadOnlyDictionary<string, string> Texts, double SpouseAffection = 0, double SpouseTrust = 0);

/// <summary>Uma categoria do menu (amigável, romântica, má...): <c>Name</c> é o que o jogador lê.</summary>
public sealed record SocialCategory(string Id, string Name);

/// <summary>O estado da relação do par, em ordem (a primeira que casa vence): <c>Spouse</c> exige o casamento; os mínimos olham
/// o que a outra pessoa sente por quem pergunta (afeto e confiança, 0–100); <c>AffectionMax</c> limita por cima (inimizade).</summary>
public sealed record RelationStateDef(
    string Id, string Label, bool Spouse = false, double AffectionMin = 0, double TrustMin = 0, double? AffectionMax = null);

/// <summary>Humor da pessoa: nota 0–100 = 50 + <c>ValenceWeight</c> × emoção líquida (−1..1) + soma dos <c>Needs</c> (peso × (nível − 50) / 50); o
/// rótulo é o primeiro de <c>Labels</c> cujo <c>Below</c> a nota não passa.</summary>
public sealed record MoodDef(double ValenceWeight, IReadOnlyList<MoodNeedTerm> Needs, IReadOnlyList<MoodLabel> Labels);

public sealed record MoodNeedTerm(string Need, double Weight);

public sealed record MoodLabel(double Below, string Label);

/// <summary>Quando a interação vale: mínimos de afeto e confiança (do alvo por quem começa), estados da relação que a permitem (qualquer um da lista),
/// só entre adultos, e o que o alvo precisa não estar fazendo (ocupado, dormindo).</summary>
public sealed record SocialRequire(
    double AffectionMin = 0, double TrustMin = 0, IReadOnlyList<string>? States = null, bool AdultsOnly = false, string? Reason = null);

/// <summary>Um termo da decisão de aceitar: contribuição = <c>Weight</c> × (valor − <c>From</c>). <c>Of</c> é o valor lido: <c>relation.affection</c>,
/// <c>relation.trust</c>, <c>relation.respect</c> (do alvo por quem começa), <c>target.mood</c>, <c>actor.mood</c>, <c>target.need.{id}</c>,
/// <c>actor.need.{id}</c>, <c>target.trait.{personalidade}</c> ou <c>recent.{categoria}</c> (1 se a última interação do par, dentro do histórico, foi
/// dessa categoria; senão 0). <c>Reason</c> é o motivo dito quando este termo é o que mais pesa contra.</summary>
public sealed record SocialTerm(string Of, double Weight, double From = 50, string? Reason = null);

/// <summary>A regra de aceitação: chance (0–100) = <c>Base</c> + soma dos termos; o motor sorteia pelo RNG semeado do mundo. 100 ou mais sempre aceita, 0 ou menos
/// nunca. <c>RefuseReason</c> vale quando nenhum termo pesa contra.</summary>
public sealed record SocialAccept(double Base, IReadOnlyList<SocialTerm> Terms, string? RefuseReason = null);

/// <summary>Efeitos de um lado: o que o lado sente pelo outro (<c>Affection</c>, <c>Trust</c>, <c>Respect</c>; pontos 0–100), o que muda nas necessidades
/// (<c>Needs</c>, pontos de uma vez) e as emoções despertadas (<c>Mood</c>: grief, fear, anger, joy, shame; 0–1) com a marca na memória (<c>Salience</c>).</summary>
public sealed record SocialSide(
    double Affection = 0, double Trust = 0, double Respect = 0, IReadOnlyList<SocialNeedShift>? Needs = null, IReadOnlyDictionary<string, double>? Mood = null, int Salience = 0);

public sealed record SocialNeedShift(string Need, double Amount);

/// <summary>Os efeitos da interação: <c>Actor</c> (quem começou) e <c>Target</c> (quem recebeu). Valem por inteiro ao fim; interrompida, na fração vivida.</summary>
public sealed record SocialEffects(SocialSide Actor, SocialSide Target);

/// <summary>Atividade conjunta: dois corpos usam peças da casa ao mesmo tempo (<c>Use</c> o de quem convida, <c>PartnerUse</c> o do convidado; pode ser a
/// mesma peça se ela admite os dois, ou duas peças com o uso).</summary>
public sealed record SocialJoint(string Use, string PartnerUse);

/// <summary>Iniciativa de quem ninguém conduz: <c>Weight</c> (peso na escolha), <c>ToPlayer</c> (pode propor a quem o jogador conduz) e a necessidade que a
/// move (<c>Need</c>, nível abaixo do qual vale; vazio = a de <see cref="SocialRules.InitiateNeed"/>). Sem o bloco, só o jogador começa a interação.</summary>
public sealed record SocialInitiative(double Weight, bool ToPlayer = true, string? Need = null, double? Below = null, double AffectionMin = 0);

/// <summary>Uma interação: nome, categoria, quando vale, quanto dura (<c>DurationSec</c>, segundos de mundo; <c>Joint</c> usa o do uso), o gesto de cada lado,
/// como o alvo decide, o que muda, o que muda se recusa e as frases (<c>accepted</c>, <c>refused</c> com <c>{actor}</c>, <c>{target}</c>, <c>{delta}</c>).</summary>
public sealed record SocialInteractionDef(
    string Id, string Name, string Category, int DurationSec, string? Pose, string? TargetPose,
    SocialRequire Requires, SocialAccept Accept, SocialEffects Effects,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SocialEffects? OnRefuse = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SocialJoint? Joint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SocialInitiative? Initiative = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AcceptedText = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefusedText = null);
