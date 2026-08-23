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

    private readonly List<NpcId> _poolNpcIds;

    /// <summary>Ids reservados e ainda não materializados de <see cref="AggregatePool"/> (T50,
    /// reabre CITY-05 AC2) — sempre <c>_poolNpcIds.Count == AggregatePool.Count</c>. Cada membro
    /// do pool passa a ter identidade estável clicável antes de materializar, mantida em lockstep
    /// pelos mesmos métodos que já mexem em <see cref="AggregatePool"/> (nenhum ponto de
    /// crescimento/encolhimento novo, só os que já existiam: carga de cenário, materializar,
    /// desmaterializar, emigrar, extrair pool inteiro na fundação).</summary>
    public IReadOnlyList<NpcId> PoolNpcIds => _poolNpcIds;

    public City(
        CityId id, CellCoord location, long foundedAtTick, CityId? foundedFromCityId,
        AggregatePopulationPool aggregatePool,
        IReadOnlyDictionary<ResourceType, long>? stock = null,
        IReadOnlyList<ConstructionProject>? constructionQueue = null,
        long? foundingScheduledAtTick = null,
        IReadOnlyList<ReportState>? canonSlots = null,
        string name = "",
        IReadOnlyList<NpcId>? poolNpcIds = null)
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
        _poolNpcIds = (poolNpcIds ?? []).ToList();
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

    /// <summary>Remove uma obra concluída da fila — dynamic-city-growth AD-007: uma obra
    /// travada por escassez de terra pode ficar presa em qualquer posição da fila (não só a
    /// cabeça), então o chamador (<see cref="ConstructionSystem"/>) precisa remover a instância
    /// específica, não sempre o índice 0. Igualdade por referência (<see cref="ConstructionProject"/>
    /// não sobrescreve <c>Equals</c>) já basta pra identificar a instância certa.</summary>
    public void RemoveConstructionProject(ConstructionProject project) => _constructionQueue.Remove(project);

    /// <summary>Materializar debita exatamente 1 do <see cref="AggregatePool"/>, as massas
    /// informadas, e remove <paramref name="id"/> de <see cref="PoolNpcIds"/> — falha sem mutar
    /// quando <paramref name="id"/> não está reservado neste pool (T50: id precisa ser um dos
    /// reservados, não um <see cref="NpcId"/> qualquer).</summary>
    public Result<Unit> Materialize(NpcId id, long wealth, long health)
    {
        if (AggregatePool.Count <= 0 || !_poolNpcIds.Remove(id))
            return Result<Unit>.Fail("AggregatePool: id não está reservado neste pool agregado");

        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count - 1, AggregatePool.WealthSum - wealth, AggregatePool.HealthSum - health);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Desmaterializar devolve exatamente 1 ao <see cref="AggregatePool"/>, as massas
    /// informadas, e devolve o próprio <paramref name="id"/> do NPC que está saindo pra <see
    /// cref="PoolNpcIds"/> (mesmo id, nunca um novo — T50) — sempre sucesso (o inverso de
    /// <see cref="Materialize"/> nunca esvazia nada).</summary>
    public Result<Unit> Dematerialize(NpcId id, long wealth, long health)
    {
        _poolNpcIds.Add(id);
        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count + 1, AggregatePool.WealthSum + wealth, AggregatePool.HealthSum + health);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Emigração agregada (Fase 8, T11, CITY-02): reduz <see cref="AggregatePool"/> por
    /// saída anônima — diferente de <see cref="Materialize"/>, nunca cria um <see cref="Npc"/>
    /// (ninguém "chega" em lugar nenhum, o grupo só sai da conta, incluindo os ids reservados dos
    /// que saíram — T50: descartados, nunca reaproveitados). Remove a média per-head de
    /// riqueza/saúde junto, senão a média do pool subiria artificialmente a cada emigração. Falha
    /// sem mutar se <paramref name="headcount"/> exceder o que existe no pool.</summary>
    public Result<Unit> Emigrate(long headcount)
    {
        if (headcount < 0) return Result<Unit>.Fail("headcount: deve ser >= 0");
        if (headcount > AggregatePool.Count) return Result<Unit>.Fail("AggregatePool.Count: emigração excede o pool disponível");
        if (headcount == 0) return Result<Unit>.Ok(Unit.Value);

        long wealthPerHead = AggregatePool.WealthSum / AggregatePool.Count;
        long healthPerHead = AggregatePool.HealthSum / AggregatePool.Count;

        _poolNpcIds.RemoveRange(_poolNpcIds.Count - (int)headcount, (int)headcount);
        AggregatePool = new AggregatePopulationPool(
            AggregatePool.Count - headcount,
            AggregatePool.WealthSum - wealthPerHead * headcount,
            AggregatePool.HealthSum - healthPerHead * headcount);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Extrai o <see cref="AggregatePool"/> inteiro (e os ids reservados de <see
    /// cref="PoolNpcIds"/>) e zera esta cidade (Fase 8, T13, CITY-08) — usado pela fundação de
    /// assentamento pra mover toda a massa não-materializada pra uma cidade nova sem criar nem
    /// destruir nada (quem chama deposita os valores devolvidos na cidade nova).</summary>
    public (AggregatePopulationPool Pool, IReadOnlyList<NpcId> PoolNpcIds) ExtractEntirePool()
    {
        var pool = AggregatePool;
        var ids = _poolNpcIds.ToList();
        AggregatePool = AggregatePopulationPool.Empty;
        _poolNpcIds.Clear();
        return (pool, ids);
    }
}
