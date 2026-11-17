namespace LivingWorld.Simulation.Cities.Works;

/// <summary>O que a obra levanta (AD-184): a casa de uma família ou a oficina de quem empreende.</summary>
public enum BuildKind : byte { House = 0, Workshop = 1 }

/// <summary>Sócio de uma oficina em obra (AD-184): quanto pôs; a cota se acerta quando ela abre.</summary>
public sealed class BuildShare
{
    public long Npc { get; set; }
    public long Amount { get; set; }
}

/// <summary>Obra de um prédio encomendado por alguém (AD-184), canônica, no <see cref="Streets.GroundLedger"/>: o canteiro (o
/// prédio com <c>UnderWork</c>) já ocupa o lote; a obra anda só pelas horas de quem está nela — a família e os parentes nas horas
/// livres, os vizinhos em mutirão (<see cref="Helpers"/>, sem jornal) e os diaristas pagos por quem encomenda (<see cref="Crew"/>,
/// pela oferta <see cref="OfferPm"/>, que sobe quando falta braço). Pronta, o prédio passa a existir: a família muda, a oficina
/// abre.</summary>
public sealed class BuildWork
{
    public long Id { get; set; }
    public Guid City { get; set; }
    public BuildKind Kind { get; set; }
    /// <summary>O canteiro (prédio com <c>UnderWork</c> = esta obra).</summary>
    public long Building { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Croft { get; set; }
    /// <summary>Casa que encomenda (mora nela, na casa) ou a do fundador (na oficina).</summary>
    public long Household { get; set; }
    /// <summary>Quem encomenda: o chefe da casa, ou o fundador.</summary>
    public long Owner { get; set; }
    public long HoursTotal { get; set; }
    public long HoursLeft { get; set; }
    public long HoursByCrew { get; set; }
    public long HoursByHelpers { get; set; }
    public long Paid { get; set; }
    /// <summary>Material comprado e foro pagos ao abrir (moeda), para o relatório e o cliente.</summary>
    public long Spent { get; set; }
    /// <summary>Oferta de jornal aos diaristas, em milésimos do jornal do dia (<c>PavingDecision.DayWage</c>).</summary>
    public int OfferPm { get; set; } = 1000;
    public List<long> Crew { get; set; } = [];
    public List<long> Helpers { get; set; } = [];
    public long StartTick { get; set; }
    public long LastProgressTick { get; set; }
    /// <summary>Último dia em que a oferta subiu (sobe uma vez por semana sem braço).</summary>
    public long LastRaiseTick { get; set; }
    public bool Stalled { get; set; }
    public long StartEventId { get; set; }

    // Oficina (Kind = Workshop): o que o fundador já levantou e o plano com que ela abre.
    /// <summary>Moeda guardada para a obra e o giro (paga os diaristas; o resto vira o caixa da oficina).</summary>
    public long Escrow { get; set; }
    public int LocationTypeId { get; set; }
    public int Profession { get; set; }
    public Dictionary<int, long> Prices { get; set; } = [];
    public List<BuildShare> Partners { get; set; } = [];
    /// <summary>Capital total posto (a base das cotas).</summary>
    public long Capital { get; set; }
    /// <summary>Parte do <see cref="Escrow"/> que não vai a jornal: o insumo inicial e o capital de giro com que a oficina abre.</summary>
    public long Reserve { get; set; }
    public string? PartnersText { get; set; }
    /// <summary>Empréstimo da abertura (0 = sem), que fica com a oficina como garantia quando ela abre.</summary>
    public long Loan { get; set; }
    /// <summary>Texto da origem do capital para o evento de abertura (<c>origem|necessario|proprio|herdado</c>).</summary>
    public string? Origin { get; set; }
}
