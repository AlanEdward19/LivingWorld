namespace LivingWorld.Domain;

/// <summary>Cidade (Fase 8, T1, CITY-01/CITY-04): população/riqueza/saúde/desigualdade nunca são
/// campo escrito à mão — nascem sempre de <see cref="CityPopulationQuery"/> sobre
/// <c>WorldState.Npcs</c> (filtrado por <see cref="CityId"/>) + <see cref="AggregatePool"/>.
/// Mesmo molde de <see cref="Household"/>/<see cref="Workplace"/>: construtor único de
/// reidratação.</summary>
public sealed class City
{
    public CityId Id { get; }
    public CellCoord Location { get; }
    public long FoundedAtTick { get; }
    public CityId? FoundedFromCityId { get; }

    /// <summary>Nome canônico (Fase 15.1, T44): autorado no World Creator, ou composto por
    /// <c>CityNameGenerator</c> (ADR-0013, gramática procedural) quando a simulação funda a
    /// cidade (SettlementFoundingSystem) ou quando o cenário não declara um.</summary>
    public string Name { get; }

    public AggregatePopulationPool AggregatePool { get; private set; }

    // SPEC_DEVIATION (Fase 8, fix round 1, gap 1 — CITY-01 AC1): design.md prometia estes 3
    // campos como stub vazio (task 1 só pede que "existam"). Sempre a mesma instância singleton
    // — nenhum estado a carregar, então não afeta round-trip/conservação (CITY-04 AC3).
    public CityGovernment Government => CityGovernment.Empty;
    public CityCulture Culture => CityCulture.Empty;
    public CityTechnology Technology => CityTechnology.Empty;

    // SPEC_DEVIATION (Fase 8, T10): design.md declara City.ConstructionQueue/BuildingIds, mas T1
    // (Foundation) não os incluiu. ConstructionSystem precisa de uma fila e de um estoque de
    // insumo por cidade para existir — BuildingIds não é necessário (world.Buildings já filtra
    // por Building.City, mesmo padrão de CityPopulationQuery filtrando Npcs por Npc.City).

    private readonly Dictionary<ResourceType, long> _stock;

    /// <summary>Insumo de construção da cidade (Fase 8, T10, CITY-03) — mesmo molde de
    /// <see cref="Household.Stock"/>, sem capacidade declarada nesta fase.</summary>
    public IReadOnlyDictionary<ResourceType, long> Stock => _stock;

    private readonly List<ConstructionProject> _constructionQueue;

    /// <summary>Fila FIFO de obras em progresso (Fase 8, T10, CITY-03) — <see
    /// cref="ConstructionSystem"/> só avança a cabeça da fila.</summary>
    public IReadOnlyList<ConstructionProject> ConstructionQueue => _constructionQueue;

    private readonly List<ReportState> _canonSlots;

    /// <summary>Relatos vivos no cânone desta comunidade (Fase 10, HIST-08) — no máximo
    /// <see cref="HistoryRules.CanonSizePerCommunity"/>.</summary>
    public IReadOnlyList<ReportState> CanonSlots => _canonSlots;

    // SPEC_DEVIATION (Fase 8, T13): SettlementFoundingSystem agenda um evento único (mesmo padrão
    // de MortalitySystem.SchedulePlannedDeath) e não pode agendar duas vezes pra mesma cidade
    // enquanto o primeiro ainda não disparou — sem um marcador, o Monthly Tick reagendaria todo
    // mês enquanto os limiares continuarem batidos.

    /// <summary>Tick em que a fundação de assentamento foi agendada (Fase 8, T13, CITY-08) —
    /// null enquanto nenhuma fundação estiver pendente. Impede reagendar a mesma cidade.</summary>
    public long? FoundingScheduledAtTick { get; private set; }

    public City(
        CityId id, CellCoord location, long foundedAtTick, CityId? foundedFromCityId,
        AggregatePopulationPool aggregatePool,
        IReadOnlyDictionary<ResourceType, long>? stock = null,
        IReadOnlyList<ConstructionProject>? constructionQueue = null,
        long? foundingScheduledAtTick = null,
        IReadOnlyList<ReportState>? canonSlots = null,
        string name = "")
    {
        Id = id;
        Location = location;
        FoundedAtTick = foundedAtTick;
        FoundedFromCityId = foundedFromCityId;
        AggregatePool = aggregatePool;
        Name = name;
        _stock = new Dictionary<ResourceType, long>(stock ?? new Dictionary<ResourceType, long>());
        _constructionQueue = (constructionQueue ?? []).ToList();
        _canonSlots = (canonSlots ?? []).ToList();
        FoundingScheduledAtTick = foundingScheduledAtTick;
    }

    public void SetCanonSlots(IReadOnlyList<ReportState> slots)
    {
        _canonSlots.Clear();
        _canonSlots.AddRange(slots);
    }

    public void MarkFoundingScheduled(long tick) => FoundingScheduledAtTick = tick;

    /// <summary>Sem capacidade declarada nesta fase (mesmo espírito de <see
    /// cref="Household.Deposit"/>).</summary>
    public long DepositStock(ResourceType resource, long amount) =>
        ResourceStock.Deposit(_stock, resource, amount, long.MaxValue);

    /// <summary>Falha sem mutar o estoque quando insuficiente (mesmo contrato de <see
    /// cref="Household.Withdraw"/>).</summary>
    public Result<long> WithdrawStock(ResourceType resource, long amount) => ResourceStock.Withdraw(_stock, resource, amount);

    public void EnqueueConstruction(ConstructionProject project) => _constructionQueue.Add(project);

    /// <summary>Remove a obra concluída do topo da fila — só o chamador (<see
    /// cref="ConstructionSystem"/>) sabe quando <see cref="ConstructionProject.TicksRemaining"/>
    /// chegou a 0.</summary>
    public void DequeueCompletedConstruction() => _constructionQueue.RemoveAt(0);

    // SPEC_DEVIATION: design.md descreve Materialize(NpcId)/Dematerialize(NpcId, ...stats). City
    // não guarda associação por NPC — WorldState.Npcs já resolve "quem está nesta cidade" via
    // Npc.CityId (T4), então um NpcId aqui seria estado morto sem leitor. Os métodos abaixo movem
    // só as massas (riqueza/saúde), suficiente para a garantia exigida pelo Done-when de T1
    // (decremento/incremento simétrico do pool).

    /// <summary>Materializar debita exatamente 1 do <see cref="AggregatePool"/> e as massas
    /// informadas — falha sem mutar quando não há ninguém agregado para tirar do pool.</summary>
    public Result<Unit> Materialize(long wealth, long health)
    {
        if (AggregatePool.Count <= 0)
            return Result<Unit>.Fail("AggregatePool.Count: nenhum NPC agregado disponível para materializar");

        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count - 1, AggregatePool.WealthSum - wealth, AggregatePool.HealthSum - health);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Desmaterializar devolve exatamente 1 ao <see cref="AggregatePool"/> e as massas
    /// informadas — sempre sucesso (o inverso de <see cref="Materialize"/> nunca esvazia nada).</summary>
    public Result<Unit> Dematerialize(long wealth, long health)
    {
        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count + 1, AggregatePool.WealthSum + wealth, AggregatePool.HealthSum + health);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Emigração agregada (Fase 8, T11, CITY-02): reduz <see cref="AggregatePool"/> por
    /// saída anônima — diferente de <see cref="Materialize"/>, nunca cria um <see cref="Npc"/>
    /// (ninguém "chega" em lugar nenhum, o grupo só sai da conta). Remove a média per-head de
    /// riqueza/saúde junto, senão a média do pool subiria artificialmente a cada emigração. Falha
    /// sem mutar se <paramref name="headcount"/> exceder o que existe no pool.</summary>
    public Result<Unit> Emigrate(long headcount)
    {
        if (headcount < 0) return Result<Unit>.Fail("headcount: deve ser >= 0");
        if (headcount > AggregatePool.Count) return Result<Unit>.Fail("AggregatePool.Count: emigração excede o pool disponível");
        if (headcount == 0) return Result<Unit>.Ok(Unit.Value);

        long wealthPerHead = AggregatePool.WealthSum / AggregatePool.Count;
        long healthPerHead = AggregatePool.HealthSum / AggregatePool.Count;

        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count - headcount,
            AggregatePool.WealthSum - wealthPerHead * headcount,
            AggregatePool.HealthSum - healthPerHead * headcount);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Extrai o <see cref="AggregatePool"/> inteiro e zera esta cidade (Fase 8, T13,
    /// CITY-08) — usado pela fundação de assentamento pra mover toda a massa não-materializada
    /// pra uma cidade nova sem criar nem destruir nada (quem chama deposita o valor devolvido na
    /// cidade nova).</summary>
    public AggregatePopulationPool ExtractEntirePool()
    {
        var pool = AggregatePool;
        AggregatePool = AggregatePopulationPool.Empty;
        return pool;
    }
}
