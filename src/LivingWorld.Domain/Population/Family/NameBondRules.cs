namespace LivingWorld.Domain.Population.Family;

/// <summary>O que o nome faz (AD-178). Xará: quem leva o nome próprio de um avô/avó ou do pai/mãe, do mesmo sexo (derivado
/// do nome e da genealogia, sem estado novo). Linhagem: os de mesmo nome de família na mesma cidade, além do parentesco que
/// a genealogia registra. Cada efeito em 0 desliga só ele; <see cref="Off"/> desliga todos. Números de calibração do modelo,
/// pequenos de propósito; nenhum tem fonte medida (precisam de conferência).</summary>
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

    public static NameBondRules Default { get; } = new();

    public static NameBondRules Off { get; } = new()
    {
        NamesakeAidGain = 0, NamesakeGriefScale = 0, NamesakeLegacyShare = 0, LineageAidCloseness = 0,
        SameFamilyCourtshipFactor = 1, LineageInfamySpill = 0,
    };
}
