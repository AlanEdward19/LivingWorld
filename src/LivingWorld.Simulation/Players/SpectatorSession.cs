using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>O que o espectador vê de uma pessoa: cópia de valores, nunca o NPC vivo.</summary>
public sealed record SpectatorNpcView(
    long Id, string Name, bool Alive, int AgeYears, string? Action, int X, int Y, Guid CityId, bool Missing);

/// <summary>O que o espectador vê de uma cidade.</summary>
public sealed record SpectatorCityView(Guid Id, string Name, int Population);

/// <summary>Modo espectador (Fase 25, AD-196): acompanha o mundo sem encarnar ninguém e sem superfície de escrita. Só lê e devolve
/// cópias (<see cref="SpectatorNpcView"/>, <see cref="SpectatorCityView"/>); não guarda estado no mundo, não sorteia do RNG do mundo e
/// não emite evento, então assistir não muda o hash. Vê o que o motor sabe (a verdade), como as rotas de leitura de hoje; se deve
/// ver só a crença de alguém é decisão pendente do dono.</summary>
public sealed class SpectatorSession(WorldState world)
{
    /// <summary>Quem o espectador está acompanhando agora (estado da sessão, fora do mundo).</summary>
    public long? FollowingId { get; private set; }

    public SpectatorNpcView? Follow(NpcId id)
    {
        FollowingId = id.Value;
        return Look(id);
    }

    public SpectatorNpcView? Look(NpcId id) => world.FindNpc(id) is { } npc
        ? new SpectatorNpcView(npc.Id.Value, npc.Name, npc.IsAlive, npc.AgeYears(world.CurrentDate), npc.CurrentAction?.ToString(),
            npc.CurrentLocation.X, npc.CurrentLocation.Y, npc.City.Value, npc.DisappearedAtTick is not null)
        : null;

    /// <summary>Quem está vivo a até <paramref name="radius"/> células (Chebyshev) de <paramref name="center"/>, por id.</summary>
    public IReadOnlyList<SpectatorNpcView> Around(CellCoord center, int radius) =>
        world.Npcs
            .Where(n => n.IsAlive && Math.Max(Math.Abs(n.CurrentLocation.X - center.X), Math.Abs(n.CurrentLocation.Y - center.Y)) <= radius)
            .OrderBy(n => n.Id.Value)
            .Select(n => Look(n.Id)!)
            .ToList();

    public SpectatorCityView? LookCity(CityId id) => world.FindCity(id) is { } city
        ? new SpectatorCityView(city.Id.Value, city.Name, world.Npcs.Count(n => n.IsAlive && n.City == city.Id))
        : null;
}
