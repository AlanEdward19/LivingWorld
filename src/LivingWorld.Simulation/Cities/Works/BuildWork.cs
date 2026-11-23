namespace LivingWorld.Simulation.Cities.Works;

/// <summary>O que a obra levanta (AD-184): a casa de uma família ou a oficina de quem empreende.</summary>
public enum BuildKind : byte { House = 0, Workshop = 1 }

/// <summary>AD-195: uma viagem de material de alguém da obra: vai à fonte (a despensa da casa, a banca de quem vende ou a mata), pega
/// a carga nas mãos e volta ao lote. <see cref="Stage"/> 0 = indo à fonte, 1 = voltando com a carga.</summary>
public sealed class HaulTrip
{
    public long Npc { get; set; }
    public int Stage { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>casa, banca ou natureza.</summary>
    public string Source { get; set; } = "natureza";
    public long Seller { get; set; } = -1;
    public long Units { get; set; }
    public long GatherDone { get; set; }
    public long GatherHours { get; set; }
    /// <summary>Quando a viagem foi dada (indo há dias sem chegar à fonte, ela é desfeita e outra é dada).</summary>
    public long Started { get; set; }
    /// <summary>Já chegou à fonte ao menos uma vez (só a viagem que nunca chega é desfeita).</summary>
    public bool Reached { get; set; }
}

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

    // AD-194: material de verdade no canteiro (com StreetRules.MaterialOnSite). Zero/nulo = obra aberta antes (material em horas).
    /// <summary>Recurso do material e unidades que a obra leva no total.</summary>
    public int MaterialResource { get; set; }
    public long MaterialNeeded { get; set; }
    /// <summary>Unidades no lote ainda não postas na parede, e as já postas.</summary>
    public long MaterialOnSite { get; set; }
    public long MaterialPlaced { get; set; }
    /// <summary>Horas de carregar já feitas desde a última carga que chegou (a viagem em curso).</summary>
    public long HaulBanked { get; set; }
    /// <summary>Travada por falta de material (evento uma vez por episódio) e perdendo o dia pelo tempo (idem).</summary>
    public bool MaterialShort { get; set; }
    public bool WeatherLost { get; set; }
    /// <summary>AD-195: viagens de material em curso (quem foi buscar e para onde).</summary>
    public List<HaulTrip> Hauls { get; set; } = [];
    /// <summary>AD-195: ajudantes que dão dia inteiro de mutirão (a família da casa e quem quer muito ajudar).</summary>
    public List<long> Mutirao { get; set; } = [];

    // Oficina (Kind = Workshop): o que o fundador já levantou e o plano com que ela abre.
    /// <summary>Moeda guardada para a obra e o giro (paga os diaristas; o resto vira o caixa da oficina).</summary>
    public long Escrow { get; set; }
    public int LocationTypeId { get; set; }
    public int TradeProfession { get; set; }
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
