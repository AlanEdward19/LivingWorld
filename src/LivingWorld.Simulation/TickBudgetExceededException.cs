namespace LivingWorld.Simulation;

/// <summary>Um tick não converge — evento se re-agenda para o mesmo tick indefinidamente
/// (task 10). Aborta nomeando o sistema culpado em vez de travar o processo.</summary>
public sealed class TickBudgetExceededException(string systemName, int maxIterations)
    : Exception($"Tick não convergiu em {maxIterations} iterações internas. Sistema culpado: {systemName}.")
{
    public string SystemName { get; } = systemName;
}
