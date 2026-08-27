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

    /// <summary>REALISM-17/18/24: um round — dano acumulado via Resolver/DamageOf, morte imediata,
    /// fuga abaixo do limiar; teto de rounds força resolução (nunca loop infinito).</summary>
    public static CombatRoundOutcome ProcessRound(WorldState world, CombatEncounterId id, TickContext tick)
    {
        var encounter = world.FindCombatEncounter(id)
            ?? throw new InvalidOperationException($"CombatEncounter {id.Value} não encontrado");
        if (encounter.Status != CombatEncounterStatus.Active)
            return OutcomeOf(encounter.Status);

        var attacker = world.FindNpc(encounter.Attacker)
            ?? throw new InvalidOperationException($"Attacker {encounter.Attacker.Value} ausente");
        var defender = world.FindNpc(encounter.Defender)
            ?? throw new InvalidOperationException($"Defender {encounter.Defender.Value} ausente");

        if (!attacker.IsAlive || !defender.IsAlive)
            return FinishResolved(world, tick, encounter, attacker, defender);

        var rules = world.CombatRules;
        int roundNumber = encounter.RoundsElapsed + 1;

        // Fuga antes do golpe se abaixo do limiar (REALISM-24).
        if (TryFlee(world, tick, encounter, attacker, defender, rules, roundNumber))
            return CombatRoundOutcome.Fled;

        ApplyStrike(world, tick, encounter, attacker, defender, isAttackerStrike: true, roundNumber);
        if (defender.Health <= 0)
        {
            NpcDeath.Apply(world, tick, defender, WorldEventKind.Death);
            return FinishResolved(world, tick, encounter with { RoundsElapsed = roundNumber }, attacker, defender);
        }

        ApplyStrike(world, tick, encounter, defender, attacker, isAttackerStrike: false, roundNumber);
        if (attacker.Health <= 0)
        {
            NpcDeath.Apply(world, tick, attacker, WorldEventKind.Death);
            return FinishResolved(world, tick, encounter with { RoundsElapsed = roundNumber }, attacker, defender);
        }

        var advanced = encounter with { RoundsElapsed = roundNumber };
        tick.LogEvent(
            WorldEventKind.CombatRound,
            $"{id.Value}|{roundNumber}|{attacker.Health}|{defender.Health}",
            sourceSystem: SystemName);

        // Teto de rounds: força resolução por exaustão (nunca trava).
        if (roundNumber >= rules.MaxRounds)
        {
            var forced = advanced with { Status = CombatEncounterStatus.Resolved };
            world.ReplaceCombatEncounter(forced);
            tick.LogEvent(
                WorldEventKind.CombatResolved,
                $"{attacker.Id.Value}|{defender.Id.Value}|Exhaustion|{roundNumber}",
                sourceSystem: SystemName);
            return CombatRoundOutcome.Resolved;
        }

        world.ReplaceCombatEncounter(advanced);
        return CombatRoundOutcome.Continuing;
    }

    private static bool TryFlee(
        WorldState world, TickContext tick, CombatEncounter encounter,
        Npc attacker, Npc defender, CombatRules rules, int roundNumber)
    {
        foreach (var (candidate, other) in OrderedFleeCandidates(attacker, defender))
        {
            if (candidate.Health >= rules.FleeHealthThreshold) continue;
            string stream =
                $"combat-flee-{encounter.Id.Value}-{candidate.Id.Value}-{roundNumber}";
            double roll = tick.Rng(stream).NextDouble();
            if (roll >= rules.FleeProbability) continue;

            var fled = encounter with
            {
                RoundsElapsed = roundNumber,
                Status = CombatEncounterStatus.Fled,
            };
            world.ReplaceCombatEncounter(fled);
            tick.LogEvent(
                WorldEventKind.CombatResolved,
                $"{candidate.Id.Value}|{other.Id.Value}|Fled|{roundNumber}",
                sourceSystem: SystemName);
            return true;
        }

        return false;
    }

    private static IEnumerable<(Npc Candidate, Npc Other)> OrderedFleeCandidates(Npc attacker, Npc defender)
    {
        // Ordem por Id — determinismo (nunca Dictionary).
        if (attacker.Id.Value <= defender.Id.Value)
        {
            yield return (attacker, defender);
            yield return (defender, attacker);
        }
        else
        {
            yield return (defender, attacker);
            yield return (attacker, defender);
        }
    }

    private static void ApplyStrike(
        WorldState world, TickContext tick, CombatEncounter encounter,
        Npc striker, Npc target, bool isAttackerStrike, int roundNumber)
    {
        int difficulty = 10 + Math.Clamp((100 - target.Health) / 20, 0, 5);
        int baseCapacity = (int)Math.Clamp(
            Math.Round(striker.Vitality / 10d + striker.RateGene.Value * 5d), 0, 20);
        int strengthBonus = (int)Math.Round((AttributeMechanic.StrengthMultiplier(world, striker) - 1) * 10);
        int capacity = LuckMechanic.AdjustCapacity(
            world, striker, tick.CurrentTick, baseCapacity + strengthBonus);
        string role = isAttackerStrike ? "atk" : "def";
        string stream =
            $"combat-round-{encounter.Id.Value}-{role}-{roundNumber}";
        var resolution = Resolver.Resolve(
            difficulty, capacity, VarianceProfile.Dramatico("combat-encounter"), tick.Rng(stream));
        // Failure = esquiva (0 dano); PartialSuccess = bloqueio (meio dano) — DamageOf já mapeia.
        int damage = CombatMechanic.DamageOf(encounter.Magnitude, resolution);
        target.SetHealth(ExtraordinaryMechanicSupport.ClampNeed((long)target.Health - damage));
    }

    private static CombatRoundOutcome FinishResolved(
        WorldState world, TickContext tick, CombatEncounter encounter, Npc attacker, Npc defender)
    {
        var resolved = encounter with { Status = CombatEncounterStatus.Resolved };
        world.ReplaceCombatEncounter(resolved);
        tick.LogEvent(
            WorldEventKind.CombatResolved,
            $"{attacker.Id.Value}|{defender.Id.Value}|Resolved|{encounter.RoundsElapsed}",
            sourceSystem: SystemName);
        return CombatRoundOutcome.Resolved;
    }

    private static CombatRoundOutcome OutcomeOf(CombatEncounterStatus status) => status switch
    {
        CombatEncounterStatus.Fled => CombatRoundOutcome.Fled,
        _ => CombatRoundOutcome.Resolved,
    };
}
