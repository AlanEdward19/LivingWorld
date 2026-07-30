using LivingWorld.Domain;

namespace LivingWorld.Simulation.History;

/// <summary>Janela de memória viva (Fase 10, HIST-01 AC3): enquanto há testemunha, consulta
/// resolve com fidelidade alta e enviesada, sem converter para relato.</summary>
public static class LivingMemoryWindow
{
    private const double WitnessSignificanceBias = 0.05;

    public static bool HasLivingWitness(Fact fact, WorldState world)
    {
        foreach (var participant in fact.Participants)
        {
            if (world.FindNpc(participant) is { IsAlive: true })
                return true;
        }
        return false;
    }

    /// <summary>Visão enviesada da testemunha — nunca aplica operador de distorção.</summary>
    public static WitnessedAccount Recall(Fact fact, WorldState world)
    {
        double perceived = Math.Min(1.0, fact.Significance + WitnessSignificanceBias);
        return new WitnessedAccount(
            fact.Id,
            fact.Participants,
            fact.Kind,
            fact.Tick,
            fact.Location,
            perceived,
            fact.Payload);
    }
}
