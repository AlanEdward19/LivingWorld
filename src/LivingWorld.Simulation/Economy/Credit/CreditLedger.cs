using System.Text.Json.Serialization;

namespace LivingWorld.Simulation.Economy.Credit;

public enum LoanPurpose : byte
{
    /// <summary>Abrir a oficina (equipamento, obra, foro, giro inicial).</summary>
    Founding = 1,

    /// <summary>Fechar a folha da oficina que trabalha (capital de giro).</summary>
    Working = 2,

    /// <summary>Reabrir a oficina parada ou comprada fechada.</summary>
    Reopening = 3,

    /// <summary>Comprar comida no aperto (ano ruim, salário que não chega).</summary>
    Food = 4,
}

/// <summary>Um empréstimo em moeda (AD-155). A moeda saiu da carteira do credor (ou do tesouro da cidade, <see cref="Lender"/> = −1)
/// no dia da concessão; o que fica aqui é só o direito de receber: <see cref="Balance"/> é o que falta pagar (principal, acréscimo do
/// contrato e multas, menos o pago). <see cref="Workplace"/> é a oficina dada em garantia (0 = sem garantia: a penhora é da carteira).</summary>
public sealed class Loan
{
    public long Id { get; set; }
    public long Lender { get; set; } = -1;
    public Guid LenderCity { get; set; }
    public long Borrower { get; set; }
    public long Workplace { get; set; }
    public LoanPurpose Purpose { get; set; }
    public long Principal { get; set; }
    public long Balance { get; set; }
    public long Installment { get; set; }

    /// <summary>Acréscimo e multas já postos no saldo e o teto deles (a norma da usura).</summary>
    public long Charged { get; set; }
    public long ChargeCap { get; set; }
    public long Paid { get; set; }
    public long StartTick { get; set; }

    /// <summary>Meses seguidos sem a parcela inteira.</summary>
    public int Missed { get; set; }

    [JsonIgnore] public bool FromCity => Lender < 0;
    [JsonIgnore] public string LenderTag => FromCity ? $"city-{LenderCity}" : $"npc-{Lender}";
}

/// <summary>Calote de alguém (AD-155): a fama de mau pagador, que fecha a porta do crédito por
/// <see cref="LivingWorld.Domain.Economy.CreditRules.DefaultMemoryYears"/>. <see cref="Lender"/> −1 = a cidade.</summary>
public sealed class DefaultMark
{
    public long Npc { get; set; }
    public long Lender { get; set; } = -1;
    public long Tick { get; set; }
}

/// <summary>Salário atrasado de uma oficina de dono (AD-155): pago quando o caixa volta; na falência é o primeiro credor.</summary>
public sealed class WageArrear
{
    public long Workplace { get; set; }
    public long Npc { get; set; }
    public long Owed { get; set; }
}

/// <summary>Meses seguidos em que a folha de uma oficina de dono não fechou (a conta da falência).</summary>
public sealed class WorkplaceDistress
{
    public long Workplace { get; set; }
    public int Months { get; set; }
}

/// <summary>O livro do crédito do mundo (AD-155), em ordem de id. Fica fora do snapshot e do hash enquanto nunca houve nada nele.</summary>
public sealed class CreditLedger
{
    public long NextLoanId { get; set; } = 1;
    public List<Loan> Loans { get; set; } = [];
    public List<DefaultMark> Defaults { get; set; } = [];
    public List<WageArrear> Arrears { get; set; } = [];
    public List<WorkplaceDistress> Distress { get; set; } = [];

    [JsonIgnore]
    public bool IsEmpty => NextLoanId == 1 && Loans.Count == 0 && Defaults.Count == 0 && Arrears.Count == 0 && Distress.Count == 0;

    public Loan Add(Loan loan)
    {
        loan.Id = NextLoanId++;
        Loans.Add(loan);
        return loan;
    }

    /// <summary>Calote dentro da memória da vila; <paramref name="exceptAgainst"/> ignora o calote dado em outro (o parente perdoa o
    /// que não foi com ele).</summary>
    public bool DefaultedSince(long npc, long sinceTick, long exceptAgainst = long.MinValue)
    {
        foreach (var mark in Defaults)
            if (mark.Npc == npc && mark.Tick >= sinceTick && mark.Lender != exceptAgainst) return true;
        return false;
    }

    public void MarkDefault(long npc, long lender, long tick, long forgetBefore)
    {
        Defaults.RemoveAll(d => d.Tick < forgetBefore);
        Defaults.Add(new DefaultMark { Npc = npc, Lender = lender, Tick = tick });
    }

    public void AddArrear(long workplace, long npc, long owed)
    {
        if (owed <= 0) return;
        foreach (var a in Arrears)
            if (a.Workplace == workplace && a.Npc == npc) { a.Owed += owed; return; }
        int at = Arrears.FindIndex(a => a.Workplace > workplace || (a.Workplace == workplace && a.Npc > npc));
        Arrears.Insert(at < 0 ? Arrears.Count : at, new WageArrear { Workplace = workplace, Npc = npc, Owed = owed });
    }

    public int DistressOf(long workplace) => Distress.Find(d => d.Workplace == workplace)?.Months ?? 0;

    public void SetDistress(long workplace, int months)
    {
        int i = Distress.FindIndex(d => d.Workplace == workplace);
        if (months <= 0) { if (i >= 0) Distress.RemoveAt(i); return; }
        if (i >= 0) { Distress[i].Months = months; return; }
        int at = Distress.FindIndex(d => d.Workplace > workplace);
        Distress.Insert(at < 0 ? Distress.Count : at, new WorkplaceDistress { Workplace = workplace, Months = months });
    }
}
