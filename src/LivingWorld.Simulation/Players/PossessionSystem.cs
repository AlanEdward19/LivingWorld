using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Dono da capacidade POSSESSION (AD-197f). A disputa de vontade roda quando o possuído desperta
/// (<see cref="PossessionContest.OnWake"/>, chamado pela decisão) e o estranhamento quando o controlador age
/// (<see cref="PossessionWitness"/>): nada por tick, nada agendado; sem posse, nada roda. Este sistema só declara a capacidade e
/// seus eventos para o catálogo.</summary>
[LivingCapability("POSSESSION",
    Events = [WorldEventKind.PossessionExpelled, WorldEventKind.PossessionSuspected, WorldEventKind.PossessionConcluded, WorldEventKind.PossessionConfronted],
    Consumers = ["timeline.knowledge"])]
public sealed class PossessionSystem : ISimulationSystem
{
    public const string SystemName = "possession";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
    }
}
