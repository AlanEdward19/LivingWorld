namespace LivingWorld.Domain.Population.Family;

/// <summary>O que o nome faz (AD-178). Xará: quem leva o nome próprio de um avô/avó ou do pai/mãe, do mesmo sexo (derivado
/// do nome e da genealogia, sem estado novo). Linhagem: os de mesmo nome de família na mesma cidade, além do parentesco que
/// a genealogia registra. Cada efeito em 0 desliga só ele; <see cref="Off"/> desliga todos. Números de calibração do modelo,
/// pequenos de propósito; nenhum tem fonte medida (AD-183: confiança por número em docs/domain/names.md).</summary>
public sealed record NameBondRules
{
    /// <summary>Ganho relativo na chance diária de o xará socorrer a casa do homenageado (e vice-versa) na ajuda mútua.
    /// Lembrança: padrinho/avô homônimo como protetor preferencial na Europa pré-industrial. Precisa de conferência.</summary>
    public double NamesakeAidGain { get; init; } = 0.5;

    /// <summary>Luto do xará pela morte do homenageado, na escala do luto de amigo (0–1).</summary>
    public double NamesakeGriefScale { get; init; } = 0.6;

    /// <summary>Fração da carteira do morto deixada ao neto xará vivo antes da partilha (legado ao afilhado/xará).</summary>
    public double NamesakeLegacyShare { get; init; } = 0.1;

    /// <summary>Proximidade (0–1) que o mesmo nome de família na mesma cidade dá na ajuda mútua quando não há parentesco
    /// próximo nem afeto. Parente próximo vale 1; afeto, 0,5–1.</summary>
    public double LineageAidCloseness { get; init; } = 0.3;

    /// <summary>Multiplicador da preferência no namoro quando os dois levam o mesmo nome de família e a genealogia não acha
    /// ascendente comum: o nome é tomado por sinal de parentesco (1 = sem efeito).</summary>
    public double SameFamilyCourtshipFactor { get; init; } = 0.5;

    /// <summary>Fração da má fama do pior da linhagem na cidade que respinga em quem leva o mesmo nome de família (suspeita,
    /// troca de culpado na memória da testemunha, crédito).</summary>
    public double LineageInfamySpill { get; init; } = 0.25;

    // --- AD-183: honra (boa fama) que respinga como a vergonha ---

    /// <summary>Fração da boa fama do mais honrado da linhagem na cidade (vivo ou morto, enquanto lembrado) que respinga em
    /// quem leva o mesmo nome de família; simétrica a <see cref="LineageInfamySpill"/>. 0 desliga a honra inteira (nenhum feito
    /// é guardado).</summary>
    public double LineageHonorSpill { get; init; } = 0.25;

    /// <summary>Meia-vida (dias) da boa fama de um feito; a mesma da má fama (<c>JusticeRules.InfamyHalfLifeDays</c>).</summary>
    public int HonorHalfLifeDays { get; init; } = 1095;

    /// <summary>Boa fama (0–100) que cada socorro dado a outra casa soma a quem socorreu.</summary>
    public double HonorPerAid { get; init; } = 2;

    /// <summary>Boa fama de quem fez funcionar uma coisa nova (protótipo de design que deu certo).</summary>
    public double HonorPerInvention { get; init; } = 15;

    /// <summary>Boa fama de quem abriu o próprio negócio.</summary>
    public double HonorPerVenture { get; init; } = 5;

    /// <summary>Boa fama de quem liderou a saída e fundou a cidade-filha (<see cref="FounderHonor"/> desliga só este feito).</summary>
    public double HonorForFounding { get; init; } = 30;

    // --- AD-186: honra de outros feitos ---

    /// <summary>Boa fama de quem chega à mestria no ofício que exerce (habilidade em <see cref="MasteryShareOfCap"/> do teto),
    /// uma vez por pessoa enquanto a fama dura; 0 desliga.</summary>
    public double HonorPerMastery { get; init; } = 10;

    /// <summary>Fração do teto da habilidade que a cidade reconhece como mestria.</summary>
    public double MasteryShareOfCap { get; init; } = 0.8;

    /// <summary>Boa fama de cada pai e mãe vivos quando um filho chega à idade adulta (criou o filho); 0 desliga.</summary>
    public double HonorPerChildRaised { get; init; } = 3;

    /// <summary>Boa fama de quem tira lucro do próprio negócio, por retirada que passa de <see cref="ProsperityWageMonths"/>
    /// salários mínimos do ofício mais barato (riqueza relativa: o lucro que se vê); 0 desliga.</summary>
    public double HonorPerProsperity { get; init; } = 2;

    /// <summary>Meses de salário mínimo que a retirada tem de passar para a cidade comentar a prosperidade.</summary>
    public double ProsperityWageMonths { get; init; } = 3;

    /// <summary>Boa fama de quem, atacado, sai de pé da luta e o agressor não (venceu ou fez fugir); 0 desliga.</summary>
    public double HonorPerCourage { get; init; } = 10;

    /// <summary>Quanto da boa fama vista desconta da má fama vista (suspeita, troca de culpado, crédito): 0,5 = dois pontos de
    /// honra apagam um de vergonha.</summary>
    public double HonorOffsetsInfamy { get; init; } = 0.5;

    /// <summary>Confiança (0–100) que a boa fama vista plena dá ao credor que não é parente (crédito).</summary>
    public double HonorCreditTrust { get; init; } = 20;

    /// <summary>Peso do nome (boa fama menos má fama vistas, −1..1) na preferência do namoro: ×(1 + peso × nome).</summary>
    public double ReputationCourtshipGain { get; init; } = 0.2;

    /// <summary>Com vaga escassa, quem tem nome melhor (boa fama menos má fama vistas) é contratado primeiro; empate por id.</summary>
    public bool ReputationHiringOrder { get; init; } = true;

    /// <summary>AD-186: quem abre negócio chama antes, entre os amigos que confiam, o sócio de nome melhor (boa fama menos má fama).</summary>
    public bool ReputationPartnerOrder { get; init; } = true;

    /// <summary>AD-183: o fundador fica registrado na cidade-filha e, se o costume manda, a cidade leva o nome dele.</summary>
    public bool FounderHonor { get; init; } = true;

    /// <summary>AD-183: quem é pedido também pesa o mesmo nome de família ao aceitar (antes só pesava na escolha do pretendente);
    /// a recusa sai como <c>MesmoNome</c>.</summary>
    public bool NameSignalOnAcceptance { get; init; } = true;

    /// <summary>AD-183: costume de nome declarado pelo cenário ou período, por cultura (bloco opcional <c>Naming</c>); a cultura
    /// sem costume declarado (e o campo que vier nulo) fica com o sorteio da cultura. Nulo = nenhum (omitido do snapshot).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<NamingCustom>? Customs { get; init; }

    public NamingCustom? CustomOf(int culture)
    {
        if (Customs is null) return null;
        foreach (var c in Customs) if (c.Culture == culture) return c;
        return null;
    }

    /// <summary>Honra ligada: algum feito guarda boa fama (sem isso o livro de honra nem nasce).</summary>
    public bool HonorOn => HonorPerAid > 0 || HonorPerInvention > 0 || HonorPerVenture > 0 || HonorForFounding > 0
        || HonorPerMastery > 0 || HonorPerChildRaised > 0 || HonorPerProsperity > 0 || HonorPerCourage > 0;

    public static NameBondRules Default { get; } = new();

    public static NameBondRules Off { get; } = new()
    {
        NamesakeAidGain = 0,
        NamesakeGriefScale = 0,
        NamesakeLegacyShare = 0,
        LineageAidCloseness = 0,
        SameFamilyCourtshipFactor = 1,
        LineageInfamySpill = 0,
        LineageHonorSpill = 0,
        HonorPerAid = 0,
        HonorPerInvention = 0,
        HonorPerVenture = 0,
        HonorForFounding = 0,
        HonorPerMastery = 0,
        HonorPerChildRaised = 0,
        HonorPerProsperity = 0,
        HonorPerCourage = 0,
        HonorOffsetsInfamy = 0,
        HonorCreditTrust = 0,
        ReputationCourtshipGain = 0,
        ReputationHiringOrder = false,
        ReputationPartnerOrder = false,
        FounderHonor = false,
        NameSignalOnAcceptance = false,
    };
}
