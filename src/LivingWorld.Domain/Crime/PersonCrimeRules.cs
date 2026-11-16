namespace LivingWorld.Domain.Crime;

/// <summary>Violência doméstica (AD-154): agressão dentro de casa (cônjuge, filho, velho) que sai do temperamento (a mesma
/// propensão da violência), do estresse (fome, miséria, desemprego), do ciúme (mágoa conjugal) e do histórico (quem apanhou
/// lembra e repete). Poucas testemunhas, denúncia rara (a norma tolera o "castigo moderado"), efeito na relação, no medo, na
/// saúde e na saída de casa (a mágoa conjugal alimenta a separação do AD-136).</summary>
public sealed record DomesticViolenceRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Chance por ano de um adulto de propensão 1, sem estresse, agredir alguém da casa.</summary>
    public double PerYear { get; init; } = 0.012;

    /// <summary>Quanto cada estresse multiplica a chance (soma: 1 + fome + miséria + desemprego).</summary>
    public double HungerStress { get; init; } = 1.5;
    public double PovertyStress { get; init; } = 1.0;
    public double UnemployedStress { get; init; } = 0.5;

    /// <summary>Ciúme: 1 + este valor × mágoa conjugal (0..1) com o cônjuge.</summary>
    public double JealousyGain { get; init; } = 2.0;

    /// <summary>Histórico: 1 + este valor × lembrança de violência sofrida (0..1), inclusive a da infância.</summary>
    public double HistoryGain { get; init; } = 1.5;

    public int MinAgeYears { get; init; } = 16;
    public int MinVictimAgeYears { get; init; } = 3;

    /// <summary>Fração do dano médio da briga (<see cref="ViolenceRules.HarmMean"/>) e golpe mortal (muito mais raro que na rua).</summary>
    public double HarmScale { get; init; } = 0.6;
    public double LethalChance { get; init; } = 0.004;

    /// <summary>Mágoa conjugal (0..100) que cada agressão soma no cônjuge agredido (pesa na separação).</summary>
    public double SpouseGrievance { get; init; } = 20;

    /// <summary>AD-162: a norma tolerava o "castigo moderado" e a separação de mesa e leito exigia crueldade excessiva provada, rara
    /// nas cortes da Igreja (Helmholz 1974, citado; grau [B]). A agressão leve e isolada soma só esta parte de
    /// <see cref="SpouseGrievance"/>; a grave (dano a partir de <see cref="SevereHarm"/>) ou a repetida (a vítima ainda guarda do
    /// mesmo agressor mágoa de ao menos <see cref="RepeatGrudgeMin"/>) soma a mágoa inteira.</summary>
    public double ToleratedGrievanceShare { get; init; } = 0.25;
    public int SevereHarm { get; init; } = 25;
    public int RepeatGrudgeMin { get; init; } = 10;

    /// <summary>Peso da criança que vê a agressão em casa: fração do impacto de violência na lembrança dela.</summary>
    public double WitnessChildImpact { get; init; } = 0.5;

    /// <summary>Denúncia da vítima: fração da chance normal (a norma da época tolera e a vítima depende de quem bate).</summary>
    public double ReportFactor { get; init; } = 0.15;

    /// <summary>Fração da chance de condenação comum (a corte olha pouco para dentro de casa).</summary>
    public double ConvictFactor { get; init; } = 0.5;
}

/// <summary>Sequestro (AD-154): de adulto ou criança, por resgate (o pobre de pouca moral contra a família que tem moeda),
/// vingança (a mágoa contra alguém leva o filho ou o próprio) ou posse (quem quer alguém que não o quer). A vítima fica presa na
/// casa do sequestrador (lugar real do mapa), comendo do que ele tem; a família busca; desfechos: resgate pago, soltura, fuga,
/// achada pela família, morte. Denúncia e julgamento pela justiça comum.</summary>
public sealed record KidnappingRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Chance por ano, para propensão 1 com motivo pleno, de planejar um sequestro.</summary>
    public double PerYear { get; init; } = 0.002;

    public int MinAgeYears { get; init; } = 18;

    /// <summary>Resgate: o sequestrador precisa ser pobre; a família da vítima precisa ter ao menos este total em moeda; pede esta
    /// fração do que a família tem.</summary>
    public long RansomMinFamilyCoin { get; init; } = 60;
    public double RansomShare { get; init; } = 0.5;

    /// <summary>Teto do resgate pedido (moeda): uns quatro meses de salário (o furto grave é ~um mês).</summary>
    public long RansomMaxCoin { get; init; } = 400;

    /// <summary>Vingança: mágoa mínima (0..100) contra alguém para levar o filho dele (ou ele).</summary>
    public int VengeanceGrudgeMin { get; init; } = 60;

    /// <summary>Posse: afeto mínimo dele por ela e afeto máximo dela por ele.</summary>
    public double PossessionAffectionMin { get; init; } = 60;
    public double PossessionRejectedBelow { get; init; } = 30;

    /// <summary>Por dia: chance de a família pagar (pedido feito e moeda à mão), de cada parente achar, da fuga (adulto ousado).</summary>
    public int RansomDemandAfterDays { get; init; } = 2;
    public double RansomPayPerDay { get; init; } = 0.25;
    public double SearchFindPerDay { get; init; } = 0.02;
    public double SuspectSearchFactor { get; init; } = 4.0;
    public double EscapePerDay { get; init; } = 0.03;
    public double ChildEscapeFactor { get; init; } = 0.3;

    /// <summary>Prazo do cativeiro (dias); ao fim, soltura ou morte (na chance pela propensão do sequestrador, maior na vingança).</summary>
    public int MaxHoldDays { get; init; } = 21;
    public double KillAtEndChance { get; init; } = 0.05;
    public double VengeanceKillFactor { get; init; } = 3.0;

    /// <summary>Mágoa de vingança dos parentes da vítima contra o sequestrador.</summary>
    public int KinGrudge { get; init; } = 80;

    public double ExecutionShare { get; init; } = 0.3;
}

/// <summary>Violência sexual (AD-154): evento abstrato do motor (autor, vítima, caso, consequências), sem cena nem descrição.
/// Sai do temperamento do autor (agressividade do corpo, pouca amabilidade e consciência), da oportunidade (vítima mais fraca,
/// sozinha) e da impunidade percebida (dissuasão). Consequências: trauma e medo duradouros, gravidez possível em vítima adulta
/// fértil pelas regras de concepção, revelação à família (vingança), denúncia rara, julgamento e má fama do autor.</summary>
public sealed record SexualViolenceRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Chance por ano, para propensão 1 e dissuasão nula, de planejar o ato.</summary>
    public double PerYear { get; init; } = 0.004;

    public int MinAgeYears { get; init; } = 15;
    public int MinVictimAgeYears { get; init; } = 4;

    /// <summary>Peso de uma vítima do mesmo sexo do autor na escolha (o resto é do outro sexo).</summary>
    public double SameSexWeight { get; init; } = 0.1;

    /// <summary>Gravidez: fração da fecundabilidade do ciclo que um ato isolado tem (só vítima adulta fértil).</summary>
    public double SingleActFecundability { get; init; } = 0.25;

    /// <summary>Chance de a vítima contar a cada parente próximo (a família sabe, sente e quer vingança).</summary>
    public double DisclosureChance { get; init; } = 0.3;
    public int KinGrudge { get; init; } = 90;

    /// <summary>Denúncia (fração da chance comum) e condenação (fração da chance comum): ambas raras na época.</summary>
    public double ReportFactor { get; init; } = 0.3;
    public double ConvictFactor { get; init; } = 0.5;

    /// <summary>Punição do condenado: forca, banimento, senão multa e açoite; contra criança, nunca só multa.</summary>
    public double ExecutionShare { get; init; } = 0.15;
    public double BanishShare { get; init; } = 0.4;
    public long Fine { get; init; } = 40;
}
