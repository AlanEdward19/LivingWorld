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

    public City(
        CityId id, CellCoord location, long foundedAtTick, CityId? foundedFromCityId,
        AggregatePopulationPool aggregatePool)
    {
        Id = id;
        Location = location;
        FoundedAtTick = foundedAtTick;
        FoundedFromCityId = foundedFromCityId;
        AggregatePool = aggregatePool;
    }

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
}
