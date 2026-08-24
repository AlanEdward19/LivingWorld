using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>
/// Ponto único de execução do estado extraordinário. T2 registra o sistema somente em mundos
/// ligados; aquisição e transições causais serão adicionadas em T4.
/// </summary>
public sealed class ExtraordinaryStateSystem : ISimulationSystem
{
    public const string SystemName = "ExtraordinaryState";
    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        // Estado autorado/resolvido é estável até uma aquisição ou transição causal existir.
    }
}
