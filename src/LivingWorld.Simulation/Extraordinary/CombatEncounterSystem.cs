using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Combate multi-round com estado persistente em <see cref="WorldState"/> (REALISM-16+).
/// Base de simulação — roda com <c>Extraordinary.Enabled == false</c> quando chamado direto.</summary>
public static class CombatEncounterSystem
{
    public const string SystemName = "combat-encounter";

    /// <summary>AD-010 / REALISM-16: cria encontro Active que persiste entre ticks.</summary>
    public static CombatEncounterId StartEncounter(
        WorldState world, NpcId attacker, NpcId defender, int magnitude, TickContext tick)
    {
        var id = world.NextCombatEncounterIdAndAdvance();
        var encounter = new CombatEncounter(
            id, attacker, defender, magnitude, RoundsElapsed: 0, CombatEncounterStatus.Active);
        world.AddCombatEncounter(encounter);
        tick.LogEvent(
            WorldEventKind.CombatEncounterStarted,
            $"{id.Value}|{attacker.Value}|{defender.Value}",
            sourceSystem: SystemName);
        return id;
    }
}
