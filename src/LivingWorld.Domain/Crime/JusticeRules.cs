namespace LivingWorld.Domain.Crime;

/// <summary>Quem julga no cenário: ninguém (só a vingança), a assembleia da vila (júri de vizinhos) ou o senhor (corte senhorial).</summary>
public enum JusticeAuthority
{
    None = 0,
    Assembly = 1,
    Lord = 2,
}

/// <summary>Justiça conforme a época (AD-150): testemunha que vê ou não (dormindo, de costas) e às vezes aponta a pessoa errada;
/// boato de pessoa a pessoa que distorce (troca o acusado, aumenta o valor); denúncia ou silêncio (parente e amigo calam,
/// quem tem medo do agressor cala); julgamento no dia de corte; multa com restituição, castigo no corpo, banimento e forca;
/// impunidade quando ninguém viu. A punição dissuade: cada uma sobe o medo da cidade, que decai, e quem foi punido lembra.</summary>
public sealed record JusticeRules
{
    public bool Enabled { get; init; } = true;
    public JusticeAuthority Authority { get; init; } = JusticeAuthority.Assembly;

    /// <summary>Chance de uma testemunha acordada ver quem foi; dormindo, <see cref="SleepingSeeChance"/>.</summary>
    public double WitnessSeeChance { get; init; } = 0.35;
    public double SleepingSeeChance { get; init; } = 0.08;

    /// <summary>Chance de quem viu apontar outra pessoa (memória imperfeita): um conhecido dele ou alguém de má fama.</summary>
    public double MisidentifyChance { get; init; } = 0.12;

    /// <summary>Sem testemunha, a vítima suspeita de quem tem má fama: chance por ponto de má fama (0..100) do suspeito.</summary>
    public double SuspicionPerInfamy { get; init; } = 0.006;

    /// <summary>Chance base de denunciar quem se sabe culpado; calam parente e afeto acima de <see cref="SilenceAffection"/>.</summary>
    public double ReportChance { get; init; } = 0.7;
    public double SilenceAffection { get; init; } = 70;

    /// <summary>Furto miúdo (valor abaixo deste) quase não vai à corte: a denúncia sai com esta fração da chance.</summary>
    public long PettyTheftValue { get; init; } = 10;
    public double PettyReportFactor { get; init; } = 0.25;

    /// <summary>Quanto o medo (0..1) e a má fama violenta do acusado (0..1) cortam a denúncia.</summary>
    public double FearSilence { get; init; } = 0.6;

    /// <summary>Boato: conversas por dia de cada pessoa que sabe, chance de distorcer a cada passagem, teto de quem sabe e vida.</summary>
    public double RumorTellsPerDay { get; init; } = 0.5;
    public double RumorDistortChance { get; init; } = 0.15;
    public int RumorMaxKnowers { get; init; } = 40;
    public int RumorLifeDays { get; init; } = 90;

    /// <summary>Corte a cada tantos dias (corte senhorial de três em três semanas); caso não julgado em <see cref="CaseLifeDays"/> prescreve.</summary>
    public int CourtEveryDays { get; init; } = 21;
    public int CaseLifeDays { get; init; } = 365;

    /// <summary>Condenação: base, por testemunha que confirma, pelo flagrante e pela má fama (viés: o mal-afamado é condenado mais).</summary>
    public double ConvictBase { get; init; } = 0.15;
    public double ConvictPerWitness { get; init; } = 0.3;
    public double ConvictCaught { get; init; } = 0.5;
    public double ConvictInfamy { get; init; } = 0.3;

    /// <summary>Multa: vezes o valor levado (além de devolver); quem não paga vai ao castigo.</summary>
    public double FineMultiple { get; init; } = 1.0;
    public long AssaultFine { get; init; } = 20;

    /// <summary>Valor acima do qual o furto é grave (castigo no corpo; grand larceny), ~um mês de salário; reincidente grave é banido.</summary>
    public long GraveTheftValue { get; init; } = 100;
    public int BanishAfterConvictions { get; init; } = 4;

    /// <summary>Dano do castigo no corpo (pelourinho, açoite), em pontos de saúde.</summary>
    public int CorporalHarm { get; init; } = 12;

    /// <summary>Homicida condenado: chance de forca (senão banimento); acusado de homicídio que foge antes da corte.</summary>
    public double ExecutionShare { get; init; } = 0.4;
    public double HomicideFleeChance { get; init; } = 0.5;

    /// <summary>Dissuasão: quanto cada punição sobe o medo da cidade (0..1) e a meia-vida desse medo; peso na decisão.</summary>
    public double PunishmentDeterrence { get; init; } = 0.12;
    public int DeterrenceHalfLifeDays { get; init; } = 365;
    public double DeterrenceWeight { get; init; } = 0.5;

    /// <summary>Má fama (0..100) que cada crime sabido soma ao acusado; decai à metade em <see cref="InfamyHalfLifeDays"/>.</summary>
    public int InfamyPerCrime { get; init; } = 25;
    public int InfamyHalfLifeDays { get; init; } = 1095;
}
