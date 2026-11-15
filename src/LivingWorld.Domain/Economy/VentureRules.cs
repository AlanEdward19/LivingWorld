namespace LivingWorld.Domain.Economy;

/// <summary>Quem abre o próprio negócio e com que dinheiro (AD-152, backlog 04 fatias 4 e 5). Todo número é dado de cenário com
/// padrão aqui; nenhum tem fonte firme (ver <c>docs/domain/venture.md</c>). Nulo em <see cref="EconomyRules.Venture"/> é o modelo
/// de antes: nenhum NPC empreende e a cidade abre toda oficina que falta.</summary>
public sealed record VentureRules
{
    /// <summary>Ofícios que uma pessoa pode abrir por conta própria (tipos de local). A terra da fazenda fica de fora: é do senhorio
    /// ou da comuna, e comprar ou arrendar terra é outra fatia.</summary>
    public IReadOnlyList<int> LocationTypeIds { get; init; } = [];

    /// <summary>Idade mínima do fundador: o oficial que já pode ser mestre.</summary>
    public int MinAgeYears { get; init; } = 21;

    /// <summary>Habilidade mínima (0–cap) no ofício da oficina: ninguém abre uma forja sem saber forjar.</summary>
    public double MinSkill { get; init; } = 30;

    /// <summary>Chance mensal de decidir abrir com oportunidade plena, ambição máxima e nenhum medo do risco; a chance real é esta
    /// vezes a oportunidade percebida vezes <c>Ambição/100 × (1 − AversãoAoRisco/100)</c>.</summary>
    public double MonthlyChance { get; init; } = 0.25;

    /// <summary>Quanto o recém-chegado cobra a menos que o concorrente que já vende na cidade, para ter freguesia.</summary>
    public double UndercutShare { get; init; } = 0.1;

    /// <summary>Gente com que a oficina abre (o fundador trabalha nela) e o teto de vagas dela: uma oficina de mestre, não uma fábrica.</summary>
    public int StartCrew { get; init; } = 1;
    public int MaxVacancies { get; init; } = 4;

    /// <summary>Foro do lote pago ao senhor ou à comuna (tesouro da cidade), em meses do salário do ofício.</summary>
    public double PlotFeeWageMonths { get; init; } = 6;

    /// <summary>Dias-homem da obra, pagos aos braços da cidade ao salário diário mais baixo; quem constrói é quem está sem trabalho
    /// (depois os adultos da casa do fundador). Sem ninguém, o fundador levanta a oficina sozinho e não paga obra.</summary>
    public int BuildPersonDays { get; init; } = 60;
    public int BuildCrew { get; init; } = 3;

    /// <summary>Equipamento comprado de quem o faz na cidade (para a forja: bigorna, martelos, tenazes) e que vira capital fixo (sai
    /// do estoque à venda). O que a cidade não tem pronto vem de fora pelo mercado da cidade, ao preço de teto. 0 = sem equipamento.</summary>
    public int EquipmentResourceId { get; init; }
    public long EquipmentUnits { get; init; }

    /// <summary>Capital de giro que entra no caixa da oficina: meses da folha inicial.</summary>
    public double WorkingCapitalMonths { get; init; } = 6;

    /// <summary>Reserva que o fundador e o sócio guardam para a casa, em meses do salário mais baixo, vezes <c>0,5 + Aversão/100</c>.</summary>
    public double ReserveWageMonths { get; init; } = 6;

    /// <summary>Sócio: parente (fora o cônjuge, cuja carteira já conta como do casal) ou amigo cuja confiança no fundador passa
    /// disto (0–100). Entra com até <c>(1 − Aversão/100)</c> do que tem acima da reserva e recebe a cota do lucro na proporção.</summary>
    public double MinPartnerTrust { get; init; } = 60;
    public int MaxPartners { get; init; } = 2;

    /// <summary>AD-155 (E22): limiar da escolha entre sociedade e empréstimo de quem financia (ver <c>VentureCapital.PrefersPartnership</c>).</summary>
    public double PartnershipAppeal { get; init; } = 0.5;

    /// <summary>AD-155 (E20): o dono não troca a própria oficina que paga por um emprego melhor. Falso: o modelo do AD-152.</summary>
    public bool OwnerKeepsShop { get; init; } = true;

    public static VentureRules Default { get; } = new();
}
