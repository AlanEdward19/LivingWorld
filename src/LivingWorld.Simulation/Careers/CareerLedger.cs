namespace LivingWorld.Simulation.Careers;

/// <summary>#263: o emprego de uma pessoa: a carreira e o nível (do catálogo <c>Careers</c>), o empregador (um <c>Workplace</c> com caixa: quem paga é ele), a
/// sequência de bons e maus turnos (promoção e rebaixamento), as faltas seguidas e, durante o turno, o instante em que o corpo volta (<see cref="ReturnMs"/> &gt; 0 =
/// fora, no trabalho). Nada de sistema paralelo: o salário sai do <c>Treasury</c> do empregador para a carteira (<c>Npc.Wallet</c>), a cidade retém a mesma alíquota da folha.</summary>
public sealed class CareerJob
{
    public long Npc { get; set; }
    public string Career { get; set; } = "";
    public int Level { get; set; }
    public long Employer { get; set; }
    public int Good { get; set; }
    public int Bad { get; set; }
    public int Absences { get; set; }

    /// <summary>Desempenho do último turno (0–100); -1 enquanto nunca trabalhou.</summary>
    public int Score { get; set; } = -1;
    public int Shifts { get; set; }
    public long Earned { get; set; }

    /// <summary>Último dia (do calendário do mundo) com turno cumprido ou falta registrada: um turno por dia.</summary>
    public long ShiftDay { get; set; } = -1;

    /// <summary>Do turno em curso: a saída, a chegada (nunca antes do começo), o fim do trabalho, a volta e o desempenho já medido na saída. 0 = em casa.</summary>
    public long DepartMs { get; set; }
    public long ArriveMs { get; set; }
    public long WorkEndMs { get; set; }
    public long ReturnMs { get; set; }
    public int PendingScore { get; set; }
}

/// <summary>#263: a conta de uma casa (<c>Household</c>) para um tipo de conta do catálogo: o uso medido desde a última emissão, a conta aberta (valor, multa, vencimento),
/// o próximo dia de emissão e se o serviço está cortado.</summary>
public sealed class BillState
{
    public long Household { get; set; }
    public string Bill { get; set; } = "";
    public double Usage { get; set; }
    public long Open { get; set; }
    public long Fee { get; set; }
    public long DueMs { get; set; }
    public long NextIssueDay { get; set; }
    public bool Warned { get; set; }
    public bool Cut { get; set; }
    public long Paid { get; set; }
}

/// <summary>#263: um aviso para a tela (a casa toda quando <see cref="Npc"/> é 0): pagamento, conta, promoção, hora do trabalho. <see cref="Id"/> cresce sempre, o cliente
/// mostra o que ainda não viu; <see cref="Verb"/> é a ação que o aviso oferece (<c>go</c>, <c>pay</c>).</summary>
public sealed class CareerNotice
{
    public long Id { get; set; }
    public long Ms { get; set; }
    public long Npc { get; set; }
    public long Household { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
    public long Amount { get; set; }
    public string? Verb { get; set; }
}

/// <summary>#263: uma busca de emprego feita (o jornal, o computador): vale por <c>searchValidHours</c> e lista as vagas do mundo naquele momento.</summary>
public sealed class JobSearched
{
    public long Npc { get; set; }
    public long AtMs { get; set; }
}

/// <summary>#263: o livro de carreiras e contas do mundo (esparso, ordenado por pessoa e por casa). Nulo no mundo enquanto o cenário não emprega ninguém: fora do
/// snapshot e do hash. Quem muda é o motor (turno, pagamento, conta); o cliente só mostra o que ele diz.</summary>
public sealed class CareerLedger
{
    public List<CareerJob> Jobs { get; set; } = [];
    public List<BillState> Bills { get; set; } = [];
    public List<CareerNotice> Notices { get; set; } = [];
    public List<JobSearched> Searches { get; set; } = [];
    public long NextNotice { get; set; } = 1;

    public CareerJob? JobOf(long npc)
    {
        foreach (var job in Jobs)
            if (job.Npc == npc) return job;
        return null;
    }

    public void AddJob(CareerJob job)
    {
        int at = Jobs.FindIndex(j => j.Npc > job.Npc);
        if (at < 0) Jobs.Add(job); else Jobs.Insert(at, job);
    }

    public void RemoveJob(long npc) => Jobs.RemoveAll(j => j.Npc == npc);

    public BillState? BillOf(long household, string bill)
    {
        foreach (var b in Bills)
            if (b.Household == household && b.Bill == bill) return b;
        return null;
    }

    public bool HasAccount(long household)
    {
        foreach (var b in Bills)
            if (b.Household == household) return true;
        return false;
    }

    public void AddBill(BillState bill)
    {
        int at = Bills.FindIndex(b => b.Household > bill.Household || b.Household == bill.Household && string.CompareOrdinal(b.Bill, bill.Bill) > 0);
        if (at < 0) Bills.Add(bill); else Bills.Insert(at, bill);
    }

    public JobSearched? SearchOf(long npc)
    {
        foreach (var s in Searches)
            if (s.Npc == npc) return s;
        return null;
    }

    public void Notify(long ms, long npc, long household, string kind, string text, long amount, string? verb, int keep)
    {
        Notices.Add(new CareerNotice { Id = NextNotice++, Ms = ms, Npc = npc, Household = household, Kind = kind, Text = text, Amount = amount, Verb = verb });
        if (Notices.Count > keep) Notices.RemoveRange(0, Notices.Count - keep);
    }
}
