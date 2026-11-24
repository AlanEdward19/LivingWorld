using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>O sumiço de alguém, seja quem for que o conduzia (Fase 25): o jogador que sai e o NPC do motor que some passam pelo
/// mesmo caminho, e por isso dão o mesmo mundo. Solta a vaga e a casa, quem tinha vínculo dá pela falta (lembrança e, AD-196, luto
/// e medo na psique na medida do vínculo), começa a procura e, se o cenário declarar, a contagem da morte presumida ou da volta ao
/// pool. O NPC segue vivo e simulado; se morrer, morre pelas regras de sempre (<c>NpcDeath</c>).</summary>
public static class Disappearance
{
    /// <summary>Vínculo (0–100) a partir do qual quem não é parente dá pela falta.</summary>
    public const double CloseBond = 60;

    public const string SourceSystem = "PlayerCommands";

    public static void Apply(WorldState world, TickContext ctx, Npc npc, string? playerId = null)
    {
        var rules = world.Players;
        var bonds = CloseBonds(world, npc, rules);
        npc.Disappear(ctx.CurrentTick);
        ReleaseJob(world, ctx, npc);
        ReleaseHome(world, ctx, npc);

        foreach (var (otherId, strength) in bonds)
        {
            var other = world.FindNpc(otherId);
            world.AddNpcMemory(
                otherId, MemoryCategory.Episodic, $"{npc.Name} desapareceu (evento {npc.Id.Value})",
                (int)Math.Round(strength), ctx.CurrentTick, [npc.Id], other?.CurrentLocation ?? npc.CurrentLocation,
                LlmRules.Default.CanonicalMemoryImportanceThreshold);
            if (rules.Enabled && other is not null) // AD-196: o sumiço dói na medida do vínculo (e o luto recolhe a rotina, AD-138)
                PsycheMechanic.Experience(world, other, LifeEventKind.KinDisappeared, npc.Id.Value, ctx.CurrentTick, strength / 100.0,
                    rules.KinDisappeared);
        }

        PlayerSearchSystem.Begin(ctx, world, npc, bonds);
        PlayerSearchSystem.ScheduleAbandonment(ctx, world, npc);
        // AD-196: o mundo não sabe quem conduzia; o jogador fica no registro de sessões do hospedeiro (PlayerSessionLog)
        string who = rules.Enabled ? "" : playerId ?? "";
        ctx.LogEvent(
            WorldEventKind.PlayerDisappeared, $"{npc.Id.Value}|{who}|{npc.City.Value}|{bonds.Count}", sourceSystem: SourceSystem);
    }

    private static void ReleaseJob(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Employer is not { } workplaceId) return;
        world.FindWorkplace(workplaceId)?.Fire(npc.Id);
        npc.Fire();
        ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{workplaceId.Value}", sourceSystem: SourceSystem);
    }

    /// <summary>Mesma saída de casa da morte (<c>NpcDeath</c>): se sobrar só criança, a redistribuição de família assume. O
    /// desaparecido ganha um domicílio unitário próprio.</summary>
    private static void ReleaseHome(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Household is not { } householdId || world.FindHousehold(householdId) is not { } household) return;

        household.RemoveMember(npc.Id);
        npc.LeaveHousehold(world.CurrentDate);
        if (!household.IsEmpty
            && !HouseholdRedistribution.HasLivingAdultOrElder(world, household, world.LifeStageRules, world.CurrentDate))
            HouseholdRedistribution.HandleOrphaned(world, ctx, household, world.LifeStageRules, world.CurrentDate);
        else
            HouseholdCleanup.DissolveIfEmpty(world, ctx, household);

        // Quem some segue vivendo em outro lugar, sozinho: o motor só come em casa, então sem domicílio próprio o desaparecido
        // morreria de fome em poucos dias e não haveria o que procurar.
        HouseholdRedistribution.CreateUnitaryHousehold(world, npc);
    }

    /// <summary>Quem dá pela falta e com que força (0–100), por id. Parente (cônjuge, pais, filhos, quem mora junto) vale 100 no
    /// AD-111; com a AD-196 vale a média entre o parentesco (100) e o laço que ele tem de fato com o sumido, quando existe (o
    /// parente que mal fala com ele sente menos, mas ainda procura), e 100 quando não há laço registrado. Fora da família, o laço a partir de <see cref="CloseBond"/>.</summary>
    internal static List<(NpcId Id, double Strength)> CloseBonds(WorldState world, Npc npc, PlayerRules rules)
    {
        var strength = new Dictionary<NpcId, double>();
        void Kin(NpcId? id)
        {
            if (id is not { } other || other == npc.Id || world.FindNpc(other) is not { IsAlive: true }) return;
            // o parentesco puxa por si (obrigação de sangue e de casamento) e o laço vivido modula: metade de cada
            strength[other] = rules.Enabled && world.Relationships.TryGetValue(new RelationshipKey(other, npc.Id), out var tie)
                ? (100 + Math.Clamp((tie.Trust + tie.Affection) / 2, 0, 100)) / 2
                : 100;
        }

        Kin(npc.Spouse);
        Kin(npc.MotherId);
        Kin(npc.FatherId);
        if (npc.Household is { } householdId && world.FindHousehold(householdId) is { } household)
            foreach (var member in household.Members) Kin(member);
        foreach (var other in world.Npcs)
            if (other.IsAlive && (other.MotherId == npc.Id || other.FatherId == npc.Id)) Kin(other.Id);

        foreach (var (key, tie) in world.Relationships)
        {
            if (key.To != npc.Id || strength.ContainsKey(key.From)) continue;
            double bond = (tie.Trust + tie.Affection) / 2;
            if (bond >= CloseBond && world.FindNpc(key.From) is { IsAlive: true }) strength[key.From] = bond;
        }

        return strength.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key.Value).Select(kv => (kv.Key, kv.Value)).ToList();
    }
}
