using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Único ponto de acesso ao <see cref="Fact"/> bruto (Fase 10, T14, HIST-15) —
/// motor/debug/autor. Nunca referenciado por handlers de jogo (HIST-17).</summary>
public static class HistoryTruthQuery
{
    public static Result<Fact> GetFact(WorldState world, FactId id)
    {
        var fact = world.FindFact(id);
        return fact is null
            ? Result<Fact>.Fail("Fact: não existe")
            : Result<Fact>.Ok(fact);
    }
}
