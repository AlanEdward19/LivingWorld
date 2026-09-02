using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Core;

/// <summary>Lista declarada de campos que só podem crescer (task 14) — cresce a cada fase nova
/// que introduzir outro contador ou grandeza conservada (massa monetária, estoque...). O teste
/// genérico (LivingWorld.Tests) reprova qualquer campo desta lista que regrida entre duas
/// amostras do mesmo mundo.</summary>
public static class MonotonicFields
{
    public static readonly IReadOnlyList<(string Name, Func<WorldState, long> Read)> WorldCounters =
    [
        ("NextEventId", w => w.NextEventId),
        ("NextHistoryEventId", w => w.NextHistoryEventId),
        ("NextNpcId", w => w.NextNpcId),
        ("NextHouseholdId", w => w.NextHouseholdId),
    ];

    /// <summary>Idade de cada NPC ainda vivo na amostra — derivada do relógio (task 2), nunca
    /// decresce enquanto o NPC segue vivo.</summary>
    public static IReadOnlyDictionary<NpcId, int> AgesOfLivingNpcs(WorldState world) =>
        world.Npcs.Where(n => n.IsAlive).ToDictionary(n => n.Id, n => n.AgeYears(world.CurrentDate));
}
