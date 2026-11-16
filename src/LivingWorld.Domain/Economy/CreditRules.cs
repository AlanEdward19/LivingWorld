namespace LivingWorld.Domain.Economy;

/// <summary>Crédito em moeda que já existe (AD-155, backlog 04 fatias 6 e 7): empréstimo de quem tem moeda sobrando a quem precisa
/// para abrir a oficina ou fechar a folha, com parcela, atraso, calote e falência. Nulo em <see cref="EconomyRules.Credit"/> é o
/// modelo de antes: ninguém empresta e a oficina parada só para. Nenhum número tem fonte firme (<c>docs/domain/credit.md</c>).
/// Norma da usura (vila cristã do século XIII-XIV): o juro aberto é proibido, então ele vem disfarçado de acréscimo fixo no
/// contrato (o que se devolve a mais), com multa pelo atraso (a "pena" que o direito canônico tolerava) e um teto total
/// (<see cref="MaxAnnualCharge"/>); parente empresta sem acréscimo.</summary>
public sealed record CreditRules
{
    /// <summary>Acréscimo do contrato por ano de prazo, sobre o principal (o juro disfarçado): quem não é parente cobra isto.</summary>
    public double AnnualCharge { get; init; } = 0.10;

    /// <summary>Acréscimo do parente: a família empresta sem cobrar.</summary>
    public double KinAnnualCharge { get; init; }

    /// <summary>Multa pelo atraso, sobre a parcela não paga do mês; o acréscimo e as multas somados nunca passam de
    /// <see cref="MaxAnnualCharge"/> por ano do prazo (o teto da usura, o que trava a espiral de juros).</summary>
    public double LatePenaltyShare { get; init; } = 0.05;
    public double MaxAnnualCharge { get; init; } = 0.20;

    /// <summary>Prazo em meses: o da abertura (equipamento, obra) e o do capital de giro (a folha de alguns meses). AD-162: o crédito
    /// de aldeia ia de um dia a um ano (Briggs 2009, p. 71, 13 casos com as duas datas; grau [B]): 12 meses.</summary>
    public int FoundingTermMonths { get; init; } = 12;
    public int WorkingTermMonths { get; init; } = 12;

    /// <summary>AD-159: carência da abertura e da reabertura, em meses: a oficina nova monta estoque e freguesia antes da primeira
    /// parcela (o prazo conta dali). Contratos de aldeia davam o primeiro pagamento na colheita ou na feira seguinte.</summary>
    public int FoundingGraceMonths { get; init; } = 6;

    /// <summary>AD-159: o prazo da abertura cresce de 12 em 12 meses até este teto quando a parcela do prazo curto não cabe no ganho
    /// (o credor de aldeia aceitava mais safras em vez de perder o negócio; o acréscimo cresce com o prazo). AD-162: só até 24, porque
    /// a abertura sempre dá o prédio da oficina em garantia (prazo além de um ano só com terra ou prédio penhorado).</summary>
    public int MaxFoundingTermMonths { get; init; } = 24;

    /// <summary>AD-159: o ganho de quem abre, para a parcela, inclui o salário dele mesmo na folha da oficina (vai para a casa dele):
    /// a parte deste salário que conta. 0 = só o lucro (venda − folha), o modelo do AD-155; a sonda mostrou que só o lucro negava 4 de
    /// 5 pedidos de abertura pela parcela.</summary>
    public double OwnWageInCapacityShare { get; init; } = 1.0;

    /// <summary>Quem abre põe ao menos a metade: o credor não financia mais que esta parte do capital, e quem tem muito medo do
    /// risco pede menos (vezes <c>1,5 − Aversão/100</c>, até 1: aversão 50 pede até a metade, 95 até ~28%).</summary>
    public double MaxLoanShare { get; init; } = 0.5;

    /// <summary>A parcela não passa desta parte do ganho do mês que a conta do negócio promete (venda − folha).</summary>
    public double MaxInstallmentShare { get; init; } = 0.5;

    /// <summary>Confiança mínima (0–100) do credor no tomador. Somam à confiança: parente (<see cref="KinTrust"/>), garantia de
    /// oficina (<see cref="CollateralTrust"/>) e o favor que o credor deve ao tomador (eixo Dívida, a reciprocidade).</summary>
    public double MinLenderTrust { get; init; } = 40;
    public double KinTrust { get; init; } = 40;
    public double CollateralTrust { get; init; } = 40;

    /// <summary>Má fama (0–100, da justiça) acima da qual ninguém de fora da família empresta.</summary>
    public double MaxInfamy { get; init; } = 30;

    /// <summary>Anos em que um calote fecha a porta do crédito (a fama de mau pagador corre a vila).</summary>
    public int DefaultMemoryYears { get; init; } = 10;

    /// <summary>Mágoa (0–100) do credor contra quem deu o calote, com a justiça ligada: a dívida vira motivo de briga.</summary>
    public int DefaultGrudge { get; init; } = 45;

    /// <summary>Meses seguidos sem pagar a parcela inteira até o calote.</summary>
    public int DefaultAfterMissedMonths { get; init; } = 6;

    /// <summary>O que a penhora deixa ao devedor: meses do salário mais baixo (o pão da casa).</summary>
    public double DistraintExemptWageMonths { get; init; } = 1;

    /// <summary>O tesouro da cidade (senhor, comuna) empresta com garantia de oficina, até esta parte do que tem.</summary>
    public double CityLendShare { get; init; } = 0.25;

    /// <summary>Capital de giro pedido quando a folha não fecha: meses da folha atual.</summary>
    public double WorkingLoanPayrollMonths { get; init; } = 3;

    /// <summary>Comida no aperto: a casa cuja despensa tem menos de <see cref="FoodLoanPantryDays"/> dias de comida por pessoa e cujo
    /// dinheiro não compra <see cref="FoodLoanDays"/> dias dela pede emprestado a parente ou conhecido o que falta (só quem tem
    /// salário: a parcela sai dele e cabe em metade dele). Prazo <see cref="FoodTermMonths"/>.</summary>
    public int FoodLoanPantryDays { get; init; } = 7;
    public int FoodLoanDays { get; init; } = 15;
    public int FoodTermMonths { get; init; } = 12;

    /// <summary>Meses seguidos de folha que não fecha até a oficina de dono fechar de verdade (falência).</summary>
    public int BankruptcyMonths { get; init; } = 6;

    public static CreditRules Default { get; } = new();
}
