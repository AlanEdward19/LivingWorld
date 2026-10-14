using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Jogador é um NPC com controlador externo (Fase 25): encarnar só grava <see cref="Npc.ControllerId"/>; a ação pedida
/// substitui a escolha autônoma na <c>BehaviorDecisionSystem</c> e passa pela mesma resolução; sair é desaparecimento, não
/// congelamento. Erros começam com <c>not_found:</c> ou <c>conflict:</c> para a borda mapear 404 e 409.</summary>
public static class PlayerCommands
{
    public const string NotFound = "not_found:";
    public const string Conflict = "conflict:";

    /// <summary>Ações do catálogo que o jogador escolhe; <c>Travel</c> nasce da resolução de outra ação e <c>UsePower</c> exige
    /// a invocação declarada da Fase 16, então nenhuma das duas é escolha direta.</summary>
    public static readonly IReadOnlySet<ActionType> Choosable =
        new HashSet<ActionType> { ActionType.Eat, ActionType.Sleep, ActionType.Work, ActionType.Socialize, ActionType.Idle, ActionType.Buy };

    /// <summary>Vínculo (0–100) a partir do qual alguém dá pela falta do desaparecido.</summary>
    public const double CloseBond = 60;

    public static Result<Unit> Incarnate(WorldState world, TickContext ctx, NpcId npcId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return Result<Unit>.Fail("playerId: obrigatório");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail($"{NotFound} NPC ausente ou morto");
        if (npc.ControllerId is not null) return Result<Unit>.Fail($"{Conflict} NPC já tem controlador");
        if (ControlledBy(world, playerId) is not null) return Result<Unit>.Fail($"{Conflict} jogador já controla outro NPC");

        var result = npc.Incarnate(playerId);
        if (!result.IsSuccess) return result;
        ctx.LogEvent(WorldEventKind.PlayerIncarnated, $"{npc.Id.Value}|{playerId}", sourceSystem: "PlayerCommands");
        return result;
    }

    /// <summary><paramref name="action"/> nulo devolve a escolha ao motor.</summary>
    public static Result<Unit> Act(WorldState world, NpcId npcId, string playerId, ActionType? action)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (action is { } chosen && !Choosable.Contains(chosen)) return Result<Unit>.Fail("action: fora do que o jogador pode escolher");
        npc.SetControlledAction(action);
        return Result<Unit>.Ok(Unit.Value);
    }

    public static Npc? ControlledBy(WorldState world, string playerId) =>
        world.Npcs.FirstOrDefault(n => n.IsAlive && n.ControllerId == playerId);

    /// <summary>Logout: solta o controlador, reabre a vaga e a casa, e quem tinha vínculo dá pela falta (memória cuja
    /// importância é a força do vínculo). O NPC segue vivo; se morrer, morre pelas regras de sempre (<c>NpcDeath</c>).</summary>
    public static Result<Npc> Logout(WorldState world, TickContext ctx, string playerId)
    {
        var npc = ControlledBy(world, playerId);
        if (npc is null) return Result<Npc>.Fail($"{NotFound} jogador não controla nenhum NPC");

        var bonds = CloseBonds(world, npc);
        npc.Disappear(ctx.CurrentTick);
        ReleaseJob(world, ctx, npc);
        ReleaseHome(world, ctx, npc);

        foreach (var (otherId, strength) in bonds)
        {
            world.AddNpcMemory(
                otherId, MemoryCategory.Episodic, $"{npc.Name} desapareceu (evento {npc.Id.Value})",
                (int)Math.Round(strength), ctx.CurrentTick, [npc.Id], world.FindNpc(otherId)?.CurrentLocation ?? npc.CurrentLocation,
                LlmRules.Default.CanonicalMemoryImportanceThreshold);
        }

        PlayerSearchSystem.Begin(ctx, world, npc, bonds);
        ctx.LogEvent(
            WorldEventKind.PlayerDisappeared, $"{npc.Id.Value}|{playerId}|{npc.City.Value}|{bonds.Count}", sourceSystem: "PlayerCommands");
        return Result<Npc>.Ok(npc);
    }

    private static void ReleaseJob(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.Employer is not { } workplaceId) return;
        world.FindWorkplace(workplaceId)?.Fire(npc.Id);
        npc.Fire();
        ctx.LogEvent(WorldEventKind.Fired, $"{npc.Id.Value}|{workplaceId.Value}", sourceSystem: "PlayerCommands");
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

    private static List<(NpcId Id, double Strength)> CloseBonds(WorldState world, Npc npc)
    {
        var strength = new Dictionary<NpcId, double>();
        void Kin(NpcId? id)
        {
            if (id is { } other && other != npc.Id && world.FindNpc(other) is { IsAlive: true }) strength[other] = 100;
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

        return strength.OrderBy(kv => kv.Key.Value).Select(kv => (kv.Key, kv.Value)).ToList();
    }
}

/// <summary>Borda dos comandos de jogador, no mesmo molde de <c>WorldAuthoringService</c>.</summary>
public sealed class PlayerService(IWorldEventSink sink)
{
    private TickContext Context(WorldState world) => new(world, world.Rng, world.Scheduler, sink);

    public Result<Unit> Incarnate(WorldState world, NpcId npcId, string playerId) =>
        PlayerCommands.Incarnate(world, Context(world), npcId, playerId);

    public Result<Unit> Act(WorldState world, string playerId, ActionType? action) =>
        PlayerCommands.ControlledBy(world, playerId) is { } npc
            ? PlayerCommands.Act(world, npc.Id, playerId, action)
            : Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");

    public Result<Npc> Logout(WorldState world, string playerId) =>
        PlayerCommands.Logout(world, Context(world), playerId);
}
