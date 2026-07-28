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

    public AggregatePopulationPool AggregatePool { get; private set; }

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

    public City(
        CityId id, CellCoord location, long foundedAtTick, CityId? foundedFromCityId,
        AggregatePopulationPool aggregatePool,
        IReadOnlyDictionary<ResourceType, long>? stock = null,
        IReadOnlyList<ConstructionProject>? constructionQueue = null)
    {
        Id = id;
        Location = location;
        FoundedAtTick = foundedAtTick;
        FoundedFromCityId = foundedFromCityId;
        AggregatePool = aggregatePool;
        _stock = new Dictionary<ResourceType, long>(stock ?? new Dictionary<ResourceType, long>());
        _constructionQueue = (constructionQueue ?? []).ToList();
    }

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
}
