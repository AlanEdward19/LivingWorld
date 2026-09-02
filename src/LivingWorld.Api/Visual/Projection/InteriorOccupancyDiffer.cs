using LivingWorld.Domain.Shared;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Diferença entre dois <see cref="InteriorSnapshot.Occupants"/> (Fase 15.1, T47) — as
/// 4 transições do bullet do backend-gaps.md: entrar, mover (mesmo andar, célula diferente),
/// trocar andar, sair. Bloco pronto para o transporte genérico de delta (T2/T3, ainda não
/// construído) consumir; esta task só garante que a transição é *observável*, não constrói o
/// pipe de tempo real.</summary>
public sealed record InteriorOccupancyDelta(
    IReadOnlyList<InteriorOccupant> Entered,
    IReadOnlyList<InteriorOccupant> Moved,
    IReadOnlyList<InteriorOccupant> ChangedFloor,
    IReadOnlyList<NpcId> Exited);

public static class InteriorOccupancyDiffer
{
    public static InteriorOccupancyDelta Diff(IReadOnlyList<InteriorOccupant> before, IReadOnlyList<InteriorOccupant> after)
    {
        var beforeByNpc = before.ToDictionary(o => o.Npc);
        var afterByNpc = after.ToDictionary(o => o.Npc);

        var entered = after.Where(a => !beforeByNpc.ContainsKey(a.Npc)).ToArray();

        var changedFloor = after
            .Where(a => beforeByNpc.TryGetValue(a.Npc, out var b) && b.Floor != a.Floor)
            .ToArray();

        var moved = after
            .Where(a => beforeByNpc.TryGetValue(a.Npc, out var b) && b.Floor == a.Floor && b.LocalCell != a.LocalCell)
            .ToArray();

        var exited = before.Where(b => !afterByNpc.ContainsKey(b.Npc)).Select(b => b.Npc).ToArray();

        return new InteriorOccupancyDelta(entered, moved, changedFloor, exited);
    }
}
