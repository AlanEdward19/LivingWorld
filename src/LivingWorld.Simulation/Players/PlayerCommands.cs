using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>Jogador é um NPC com controlador externo (Fase 25): encarnar só grava <see cref="Npc.ControllerId"/>; a ação pedida
/// substitui a escolha autônoma na <c>BehaviorDecisionSystem</c> e passa pela mesma resolução; sair é desaparecimento
/// (<see cref="Disappearance"/>, o mesmo caminho do NPC do motor que some), não congelamento. Encarnar quem estava sumido é a volta
/// tardia (<see cref="LateReturn"/>). Erros começam com <c>not_found:</c> ou <c>conflict:</c> para a borda mapear 404 e 409.</summary>
public static class PlayerCommands
{
    public const string NotFound = "not_found:";
    public const string Conflict = "conflict:";

    /// <summary>AD-197e: a pessoa possuída (posse comum) recusou a ordem; a borda responde 409 com o motivo e o mundo não muda.</summary>
    public const string Refused = "conflict: recusa:";

    /// <summary>Ações do catálogo que o jogador escolhe. <c>Travel</c> nasce da resolução de outra ação; <c>UsePower</c> (AD-196)
    /// exige um poder que o NPC porta, e a invocação sai da mesma lista de oportunidades do motor.</summary>
    public static readonly IReadOnlySet<ActionType> Choosable =
        new HashSet<ActionType>
        {
            ActionType.Eat, ActionType.Sleep, ActionType.Work, ActionType.Socialize, ActionType.Idle, ActionType.Buy, ActionType.UsePower,
        };

    /// <summary>Vínculo (0–100) a partir do qual alguém dá pela falta do desaparecido.</summary>
    public const double CloseBond = Disappearance.CloseBond;

    public static Result<Unit> Incarnate(WorldState world, TickContext ctx, NpcId npcId, string playerId,
        PossessionMode mode = PossessionMode.Common)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return Result<Unit>.Fail("playerId: obrigatório");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail($"{NotFound} NPC ausente ou morto");
        if (npc.ControllerId is not null) return Result<Unit>.Fail($"{Conflict} NPC já tem controlador");
        if (ControlledBy(world, playerId) is not null) return Result<Unit>.Fail($"{Conflict} jogador já controla outro NPC");

        if (mode == PossessionMode.Common && PossessionContest.Prevents(world, npc, ctx.CurrentTick)) // AD-197f: vontade forte não deixa entrar
            return Result<Unit>.Fail($"{Refused} a vontade de {npc.Name} impede a posse");
        if (npc.DisappearedAtTick is not null) LateReturn.Apply(world, ctx, npc, LateReturn.ByPlayer); // voltar depois de sumido
        var result = npc.Incarnate(playerId);
        if (!result.IsSuccess) return result;
        if (mode == PossessionMode.Full) npc.SetPossession(PossessionMode.Full, ctx.CurrentTick); // AD-197e: a pessoa fica presa
        else PossessionContest.Begin(world, ctx, npc); // AD-197f: a disputa de vontade começa
        // AD-196: encarnar não é fato do mundo (ninguém dentro dele percebe quem conduz): fica no registro de sessões do hospedeiro
        if (!world.Players.Enabled)
            ctx.LogEvent(WorldEventKind.PlayerIncarnated, $"{npc.Id.Value}|{playerId}", sourceSystem: "PlayerCommands");
        return result;
    }

    /// <summary><paramref name="action"/> nulo devolve a escolha ao motor. <paramref name="powerId"/> só vale com
    /// <see cref="ActionType.UsePower"/> e tem de ser um poder que o NPC porta.</summary>
    public static Result<Unit> Act(WorldState world, NpcId npcId, string playerId, ActionType? action, string? powerId = null)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (PossessedByPower(world, npc) is { } taken) return taken;
        if (action is { } chosen && !Choosable.Contains(chosen)) return Result<Unit>.Fail("action: fora do que o jogador pode escolher");
        if (action == ActionType.UsePower && !world.Players.Enabled) return Result<Unit>.Fail("action: fora do que o jogador pode escolher");
        if (action == ActionType.UsePower && !Carries(world, npc, powerId))
            return Result<Unit>.Fail("powerId: o NPC não porta esse poder");
        npc.SetControlledAction(action, powerId);
        WakeNow(world, npc); // AD-197c: o corpo conduzido obedece no próximo tick fino, não na hora cheia
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>AD-197c (ADR-0050, decisão 2): quem é tocado pelo jogador é despertado pelo evento, no próximo tick fino. De hora em
    /// hora nada muda (o despertar de sempre).</summary>
    internal static void WakeNow(WorldState world, Npc npc)
    {
        if (world.FineTime && npc.IsAlive) world.ReplaceNpcWakeAtMs(npc.Id.Value, world.CurrentDate.TotalMilliseconds + 1);
    }

    /// <summary>AD-197f: o controlador pediu <paramref name="action"/> e quem convive compara com o costume da pessoa.</summary>
    internal static void Witness(WorldState world, TickContext ctx, NpcId npcId, ActionType? action)
    {
        if (world.FindNpc(npcId) is { } npc) PossessionWitness.OnAction(world, ctx, npc, action);
    }

    /// <summary>AD-197f: quem possui tenta convencer a pessoa a ceder (posse comum): tira resistência, uma vez por hora.</summary>
    public static Result<Unit> Persuade(WorldState world, TickContext ctx, NpcId npcId, string playerId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (PossessedByPower(world, npc) is { } taken) return taken;
        return PossessionContest.Persuade(world, npc, ctx.CurrentTick)
            ? Result<Unit>.Ok(Unit.Value)
            : Result<Unit>.Fail("persuade: sem disputa de vontade agora (posse plena, regra desligada ou já tentou nesta hora)");
    }

    /// <summary>AD-197g: o personagem do jogador possuído por poder não obedece a ele enquanto a outra vontade conduz o corpo; ele
    /// resiste pela mesma regra de qualquer pessoa (<see cref="PossessionContest"/>).</summary>
    private static Result<Unit>? PossessedByPower(WorldState world, Npc npc) =>
        LivingWorld.Simulation.Extraordinary.Mechanics.ControlMechanic.IsPossessed(world, npc)
            ? Result<Unit>.Fail($"{Refused} outra vontade conduz o corpo de {npc.Name}")
            : null;

    private static bool Carries(WorldState world, Npc npc, string? powerId) =>
        !string.IsNullOrWhiteSpace(powerId)
        && world.ExtraordinaryCarriers.Any(c => c.CarrierId == npc.Id && c.PowerIds.Contains(powerId));

    public static Npc? ControlledBy(WorldState world, string playerId) =>
        world.Npcs.FirstOrDefault(n => n.IsAlive && n.ControllerId == playerId);

    /// <summary>AD-197e: um passo na direção pedida (−1..1 em cada eixo): intenção, não posição. Vale a mesma regra do passo que a
    /// rota antiga <c>/visual/player/move</c> aplicava (dentro do mapa, uma célula); a posição contínua e o custo do passo pelo corpo
    /// vêm com o tempo real (ADR-0050).</summary>
    public static Result<Unit> Move(WorldState world, TickContext ctx, NpcId npcId, string playerId, int dx, int dy)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (PossessedByPower(world, npc) is { } taken) return taken;
        if (dx is < -1 or > 1 || dy is < -1 or > 1 || (dx == 0 && dy == 0)) return Result<Unit>.Fail("move: direção inválida");
        var target = new LivingWorld.Domain.Geography.CellCoord(npc.CurrentLocation.X + dx, npc.CurrentLocation.Y + dy);
        var valid = LivingWorld.Simulation.Visibility.PlayerMovementValidator.Validate(world, npc, target);
        if (!valid.IsSuccess) return valid;
        npc.MoveTo(target, ctx.CurrentTick);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>AD-197e: golpe no alvo ao alcance (a mesma célula ou a vizinha) pelo mesmo caminho da briga de qualquer NPC
    /// (<c>Violence.Fight</c>: dano pela força, ferimento ou morte, testemunha, denúncia, justiça, mágoa, vingança). Na posse comum
    /// a pessoa recusa ferir quem ela valoriza muito (<see cref="PossessionWill"/>); na plena ela não recusa, mas sofre presa.</summary>
    public static Result<Unit> Strike(WorldState world, TickContext ctx, NpcId npcId, string playerId, NpcId targetId)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || npc.ControllerId != playerId) return Result<Unit>.Fail($"{NotFound} jogador não controla este NPC");
        if (PossessedByPower(world, npc) is { } taken) return taken;
        if (world.FindNpc(targetId) is not { IsAlive: true } target || target.Id == npc.Id)
            return Result<Unit>.Fail($"{NotFound} alvo ausente ou morto");
        // AD-197c: no passo fino o alcance é o da pose de agora (quem passa pela rua no meio da viagem está ao alcance)
        if (LivingWorld.Simulation.Behavior.Decision.TravelPose.Reach(world, npc, target) > 1)
            return Result<Unit>.Fail("strike: alvo fora do alcance");
        if (!world.CrimeOn) return Result<Unit>.Fail("strike: violência desligada no cenário");

        // o quanto ferir vai contra a pessoa: vínculo (AD-197e) e, com a disputa de vontade, também os valores (AD-197f)
        double care = PossessionContest.On(world) ? PossessionContest.HarmWeight(world, npc, target) : PossessionWill.CareFor(world, npc, target);
        bool against = world.Players.Enabled && care >= world.Players.RefuseHarmCare; // LW_AD196=off: sem vontade na posse
        // AD-197f: na posse comum é uma disputa de vontade (valores, vínculos, cansaço, controle)
        if (npc.ControlMode != PossessionMode.Full && PossessionContest.Refuses(world, npc, target, ctx.CurrentTick))
            return Result<Unit>.Fail(string.Create(CultureInfo.InvariantCulture,
                $"{Refused} {npc.Name} não fere {target.Name} (peso {PossessionContest.HarmWeight(world, npc, target):0})"));
        if (npc.ControlMode == PossessionMode.Full)
            npc.RecordCaptiveObjection(target.Id.Value, against ? (int)Math.Round(care) : 0, ctx.CurrentTick);

        long tick = ctx.CurrentTick;
        LivingWorld.Simulation.Crime.Violence.Fight(world, ctx, npc, target, "jogador",
            ctx.OneShot($"player-strike-{npc.Id.Value}-{target.Id.Value}-{tick}"));
        PossessionWitness.OnStrike(world, ctx, npc, target); // AD-197f: quem convive estranha
        WakeNow(world, target); // AD-197c: quem apanha reage já, não na hora cheia
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Logout: o NPC some pelo mesmo caminho do NPC do motor que some (<see cref="Disappearance"/>). Quem estava preso na
    /// posse plena é solto antes e vive a posse (AD-197e).</summary>
    public static Result<Npc> Logout(WorldState world, TickContext ctx, string playerId)
    {
        var npc = ControlledBy(world, playerId);
        if (npc is null) return Result<Npc>.Fail($"{NotFound} jogador não controla nenhum NPC");
        PossessionWill.Release(world, npc, ctx.CurrentTick);
        PossessionContest.End(world, npc, ctx.CurrentTick); // AD-197f: o cansaço fica e recupera
        Disappearance.Apply(world, ctx, npc, playerId);
        return Result<Npc>.Ok(npc);
    }
}
