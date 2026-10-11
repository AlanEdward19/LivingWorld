using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Behavior.Decision;

// Parte de BehaviorDecisionSystem — percepção do entorno que muda o candidato.
public sealed partial class BehaviorDecisionSystem
{
    /// <summary>PWR-56..58: considera NPCs/perigo no raio Chebyshev do portador (1 = adjacência
    /// sem poder). Perigo observável = saúde ≤ 1; percepção &gt; 1 aproxima socialmente.</summary>
    private static ActionType ApplyPerception(WorldState world, Npc npc, ActionType candidate) =>
        ApplyPerceptionWithin(world, npc, candidate, AttributeMechanic.PerceptionRadius(world, npc));

    internal static ActionType ApplyPerceptionWithin(WorldState world, Npc npc, ActionType candidate, int radius)
    {
        if (radius < 0) return candidate;

        var index = world.AliveNpcIndex;
        // Sem ninguém em perigo no mundo não há ameaça a achar; com raio ≤ 1 a mera presença de outros
        // também não muda nada (só raio > 1 aproxima socialmente): o resultado é o candidato.
        bool anyEndangered = index.EndangeredCount > 0;
        if (!anyEndangered && radius <= 1) return candidate;

        bool threat = false;
        bool otherNpc = false;
        var origin = npc.CurrentLocation;
        long window = (2L * radius + 1) * (2L * radius + 1);
        if (window >= index.Alive.Count)
        {
            // Raio grande: varrer os vivos custa menos que visitar a janela inteira de células.
            foreach (var other in index.Alive)
                if (Observe(other)) break;
        }
        else
        {
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    foreach (var other in index.At(new CellCoord(origin.X + dx, origin.Y + dy)))
                        if (Observe(other)) goto scanned;
        }
    scanned:

        // Devolve true quando o resultado já está decidido e o restante da varredura não pode mudá-lo:
        // ameaça → Travel; sem ninguém em perigo, um vizinho basta para Socializar (raio > 1).
        bool Observe(Npc other)
        {
            if (other.Id == npc.Id) return false;
            if (Chebyshev(origin, other.CurrentLocation) > radius) return false;
            otherNpc = true;
            if (other.Health <= AliveNpcIndex.EndangeredHealth)
                threat = true;
            return threat || (!anyEndangered && radius > 1);
        }

        if (threat) return ActionType.Travel;
        if (otherNpc && radius > 1) return ActionType.Socialize;
        return candidate;
    }

    private static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
