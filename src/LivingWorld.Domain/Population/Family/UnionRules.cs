namespace LivingWorld.Domain.Population.Family;

/// <summary>Vida conjugal (AD-136): desgaste, traição, fim da união em vida e o que vem depois. Os números de frequência são
/// norma da época (o padrão é a vila pré-industrial, <c>UnionRules.Medieval</c>; há também <c>UnionRules.Modern</c>). Escala de mágoa: 0..100,
/// e o afeto possível entre os dois cai na mesma proporção.</summary>
public sealed record UnionRules
{
    public bool Enabled { get; init; } = true;

    /// <summary>Mágoa por mês de convivência com temperamento em choque pleno (pouca afinidade, quem se irrita fácil).</summary>
    public double QuarrelPerMonth { get; init; } = 2.0;

    /// <summary>Mágoa por mês de penúria (despensa da casa abaixo do piso ou alguém dela passando fome).</summary>
    public double PovertyPerMonth { get; init; } = 2.5;

    /// <summary>Mágoa de cada um pela morte de um filho do casal (luto que afasta).</summary>
    public double ChildDeathGrievance { get; init; } = 8.0;

    /// <summary>Mágoa por mês de morar em casas diferentes ainda casados.</summary>
    public double AbsencePerMonth { get; init; } = 3.0;

    /// <summary>Fração da mágoa perdoada por mês (temperamento médio; a amabilidade aumenta).</summary>
    public double ForgivenessPerMonth { get; init; } = 0.08;

    /// <summary>Afeto pelo cônjuge abaixo do qual a pessoa pensa em sair.</summary>
    public double LoveLostBelow { get; init; } = 35;

    /// <summary>Chance por ano de deixar a união de quem não ama mais nada o cônjuge, temperamento médio, sem filho pequeno.</summary>
    public double SeparationPerYear { get; init; } = 0.12;

    /// <summary>Multiplicador dessa chance com filho do casal abaixo de <see cref="TenderYearsAge"/> em casa.</summary>
    public double YoungChildBarrier { get; init; } = 0.5;

    /// <summary>Multiplicador para quem não tem como se manter sozinho (sem emprego e sem dinheiro).</summary>
    public double DependencyBarrier { get; init; } = 0.3;

    /// <summary>Modo do fim: divórcio, anulação e abandono; o resto é separação de fato.</summary>
    public double DivorceShare { get; init; }
    public double AnnulmentShare { get; init; } = 0.05;
    public double AbandonmentShare { get; init; } = 0.45;

    /// <summary>Chance por ano de um casado de temperamento médio, com oportunidade, começar um caso.</summary>
    public double AffairPerYear { get; init; } = 0.05;

    /// <summary>Afeto mútuo mínimo para alguém ser oportunidade de caso.</summary>
    public double AffairAffectionMin { get; init; } = 40;

    /// <summary>Chance por mês de um caso acabar sozinho.</summary>
    public double AffairEndPerMonth { get; init; } = 0.08;

    /// <summary>Fração das concepções da mulher com caso em curso que são do amante.</summary>
    public double AffairPaternityShare { get; init; } = 0.5;

    /// <summary>Chance por mês de o cônjuge descobrir sozinho; cada confidente dele que conhece o casal do caso soma
    /// <see cref="GossipPerConfidant"/> (o boato corre pelos vínculos de afeto, até <see cref="MaxConfidants"/>).</summary>
    public double DiscoveryPerMonth { get; init; } = 0.04;
    public double GossipPerConfidant { get; init; } = 0.03;
    public int MaxConfidants { get; init; } = 5;
    public double ConfidantAffection { get; init; } = 40;

    /// <summary>Mágoa do traído na descoberta.</summary>
    public double BetrayalGrievance { get; init; } = 45;

    /// <summary>Chance de a descoberta acabar a união (temperamento médio); o resto é perdão.</summary>
    public double AdulterySeparation { get; init; } = 0.2;

    /// <summary>Chance de violência do traído contra o infiel (temperamento médio) e o dano à saúde dele.</summary>
    public double ViolenceChance { get; init; } = 0.1;
    public int ViolenceHarm { get; init; } = 25;

    /// <summary>Filho abaixo desta idade fica com a mãe na separação.</summary>
    public int TenderYearsAge { get; init; } = 7;

    /// <summary>Fração dos filhos maiores do casal que fica com o pai na separação (sorteio por filho).</summary>
    public double OlderChildrenToFather { get; init; } = 0.7;

    /// <summary>Fração da despensa do casal que quem sai leva por pessoa que vai com ele; no abandono, quem foge não leva nada.</summary>
    public double LeaverStockShare { get; init; } = 1.0;

    /// <summary>Anos até poder unir-se de novo, por modo do fim.</summary>
    public int RemarryYearsAfterSeparation { get; init; } = 5;
    public int RemarryYearsAfterAbandonment { get; init; } = 7;
    public int RemarryYearsAfterAnnulment { get; init; } = 1;

    /// <summary>Meses de luto antes de o viúvo (homem) ou a viúva cortejar de novo.</summary>
    public int WidowerMourningMonths { get; init; } = 6;
    public int WidowMourningMonths { get; init; } = 12;

    /// <summary>Parentesco (coeficiente de Wright) a partir do qual a união é tabu; 1/8 proíbe primos-irmãos, meio-irmãos, tios.</summary>
    public double ForbiddenRelatedness { get; init; } = 0.125;

    /// <summary>Gerações de ascendência consultadas para o parentesco.</summary>
    public int KinshipDepth { get; init; } = 3;

    /// <summary>Diferença de idade que cada um procura no par (o homem, mais nova; a mulher, mais velho) e a distância dela
    /// em que o interesse pela idade zera.</summary>
    public double PreferredAgeGapYears { get; init; } = 3;
    public double AgeToleranceYears { get; init; } = 15;

    public static UnionRules Medieval { get; } = new();

    /// <summary>Cenário moderno: divórcio comum e formal, guarda quase sempre com a mãe, sem espera longa.</summary>
    public static UnionRules Modern { get; } = new()
    {
        SeparationPerYear = 0.6,
        YoungChildBarrier = 0.8,
        DependencyBarrier = 0.6,
        DivorceShare = 0.95,
        AnnulmentShare = 0,
        AbandonmentShare = 0.02,
        TenderYearsAge = 18,
        OlderChildrenToFather = 0.2,
        RemarryYearsAfterSeparation = 1,
        RemarryYearsAfterAbandonment = 2,
        RemarryYearsAfterAnnulment = 0,
        AdulterySeparation = 0.5,
        ViolenceChance = 0.03,
        ForbiddenRelatedness = 0.25,
    };

    /// <summary>União para a vida (o modelo de antes): sem desgaste, caso nem separação; o tabu volta a ser só de 1º grau
    /// e não há luto.</summary>
    public static UnionRules Disabled { get; } = new()
    {
        Enabled = false,
        ForbiddenRelatedness = 0.5,
        WidowerMourningMonths = 0,
        WidowMourningMonths = 0,
    };

    public int RemarryYears(UnionEndMode mode) => mode switch
    {
        UnionEndMode.Separation => RemarryYearsAfterSeparation,
        UnionEndMode.Abandonment => RemarryYearsAfterAbandonment,
        _ => RemarryYearsAfterAnnulment,
    };

    /// <summary>Modo do fim pelo sorteio <paramref name="draw"/> em [0,1).</summary>
    public UnionEndMode ModeFor(double draw) =>
        draw < DivorceShare ? UnionEndMode.Divorce
        : draw < DivorceShare + AnnulmentShare ? UnionEndMode.Annulment
        : draw < DivorceShare + AnnulmentShare + AbandonmentShare ? UnionEndMode.Abandonment
        : UnionEndMode.Separation;

    public string? Validate()
    {
        if (DivorceShare < 0 || AnnulmentShare < 0 || AbandonmentShare < 0 || DivorceShare + AnnulmentShare + AbandonmentShare > 1)
            return "Unions: frações de modo fora de [0,1] ou somando mais que 1";
        if (ForbiddenRelatedness is <= 0 or > 1) return "Unions.ForbiddenRelatedness: fora de (0,1]";
        if (KinshipDepth < 1) return "Unions.KinshipDepth: deve ser >= 1";
        if (AffairPaternityShare is < 0 or > 1) return "Unions.AffairPaternityShare: fora de [0,1]";
        return null;
    }
}
