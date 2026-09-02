using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Domain.Cities.Spatial;

public enum NpcScopeKind { World, City }

/// <summary>Onde um NPC "está" pra fins de escopo visual (T50, bug "seguir NPC entre escopos") —
/// dentro dos bounds da própria cidade (<see cref="NpcScopeKind.City"/>) ou fora, viajando
/// (<see cref="NpcScopeKind.World"/>). <see cref="CityId"/> só é populado no caso City.</summary>
public readonly record struct NpcScope(NpcScopeKind Kind, CityId? CityId);

/// <summary>Única fonte do critério geométrico "NPC dentro/fora dos bounds da própria cidade" —
/// antes disso, <c>GlobalProjector</c> e <c>LivingScopeProjector</c> (API) cada um reimplementava
/// o mesmo <c>bounds.Contains(npc.CurrentLocation)</c> separadamente; nenhum tinha uma função
/// nomeada e nenhum expunha o resultado pro cliente (o inspector de NPC não sabia dizer "esse NPC
/// mudou de escopo" pra a câmera que o segue acompanhar).</summary>
public static class NpcScopeResolver
{
    public static NpcScope Resolve(Npc npc, CityBounds homeCityBounds) =>
        homeCityBounds.Contains(npc.CurrentLocation)
            ? new NpcScope(NpcScopeKind.City, npc.City)
            : new NpcScope(NpcScopeKind.World, null);
}
