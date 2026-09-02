namespace LivingWorld.Domain.Cities;

/// <summary>Massa de NPCs agregados (não materializados) de uma <see cref="City"/> (Fase 8,
/// approach A): contagem + somas de riqueza/saúde. Nunca cacheado por fora — só existe para o
/// `City.Materialize`/`Dematerialize` mover massa de/para um NPC real.</summary>
public readonly record struct AggregatePopulationPool(long Count, long WealthSum, long HealthSum)
{
    public static readonly AggregatePopulationPool Empty = new(0, 0, 0);
}
