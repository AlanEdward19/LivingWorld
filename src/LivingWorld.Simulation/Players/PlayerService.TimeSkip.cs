using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Players;

/// <summary>Pulo de tempo de quem joga sozinho (AD-197c, ADR-0050 decisão 3): dormindo (até <see cref="UntilMs"/>, ou acelerando até
/// descansar) ou parado, vendo o mundo andar e perdendo necessidades. Estado do hospedeiro, fora do snapshot: o que muda o mundo
/// (deitar, acordar) e a troca de Δ vão para o registro do jogador. <c>Task</c> (#241): o pulo acompanha uma ação longa do corpo
/// (<see cref="PlayerTasks"/>) e acaba com ela.</summary>
public sealed record TimeSkip(string PlayerId, long NpcId, bool Sleeping, long? UntilMs, long StartedTick, int HealthAtStart, bool Task = false);

// Parte de PlayerService — dormir e acelerar (AD-197c).
public sealed partial class PlayerService
{
    /// <summary>O pulo em curso, nulo se nenhum.</summary>
    public TimeSkip? Skip { get; private set; }

    /// <summary>Por que o último pulo acabou (golpe, fogo, fome, sede, chamado, hora, descansado...); o cliente mostra.</summary>
    public string? LastWakeReason { get; private set; }

    /// <summary>Quantos jogadores estão no mundo agora: corpos vivos com controlador.</summary>
    public static int PlayersPresent(WorldState world)
    {
        int count = 0;
        foreach (var npc in world.Npcs)
            if (npc.IsAlive && npc.ControllerId is not null) count++;
        return count;
    }

    /// <summary>O jogador pode pular o tempo: ele conduz um corpo vivo e é o único no mundo (com dois ou mais, ninguém pula).</summary>
    public static bool CanSkip(WorldState world, string playerId) =>
        world.Players.Time.FineDecisions && PlayerCommands.ControlledBy(world, playerId) is not null && PlayersPresent(world) == 1;

    /// <summary>Dormir: até o instante <paramref name="untilMs"/>, ou, nulo, acelerando até descansar. O corpo deita pela mesma ação
    /// de dormir de qualquer NPC (vai à cama se estiver longe).</summary>
    public Result<Unit> Sleep(WorldState world, string playerId, long? untilMs)
    {
        if (Refusal(world, playerId) is { } refused) return refused;
        var npc = PlayerCommands.ControlledBy(world, playerId)!;
        if (untilMs is { } until && until <= world.CurrentDate.TotalMilliseconds) return Result<Unit>.Fail("sleep: o horário já passou");
        var acted = PlayerCommands.Act(world, npc.Id, playerId, ActionType.Sleep);
        if (!acted.IsSuccess) return acted;
        Begin(world, npc, playerId, sleeping: true, untilMs);
        Record(world, playerId, PlayerCommandKind.Sleep, npc.Id.Value,
            new PlayerCommandRecord(0, 0, playerId, PlayerCommandKind.Sleep, UntilMs: untilMs));
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Acelerar parado: o corpo fica à toa e o mundo corre até o jogador agir, parar, ou algo o chamar.</summary>
    public Result<Unit> Accelerate(WorldState world, string playerId)
    {
        if (Refusal(world, playerId) is { } refused) return refused;
        var npc = PlayerCommands.ControlledBy(world, playerId)!;
        var acted = PlayerCommands.Act(world, npc.Id, playerId, ActionType.Idle);
        if (!acted.IsSuccess) return acted;
        Begin(world, npc, playerId, sleeping: false, null);
        Record(world, playerId, PlayerCommandKind.Accelerate, npc.Id.Value);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Avanço do admin (<c>step</c>, <c>advance-year</c>): <paramref name="ticks"/> ticks de <paramref name="deltaMs"/>, sem
    /// mudar o Δ em vigor. Vai para o registro antes de rodar, no instante em que começa: o replay roda os mesmos ticks.</summary>
    public Result<Unit> Advance(WorldState world, WorldClock clock, string actor, long ticks, int deltaMs)
    {
        if (ticks < 1) return Result<Unit>.Fail("advance: ao menos um tick");
        if (!WorldMillis.IsValidTickDelta(deltaMs)) return Result<Unit>.Fail($"Δ inválido: {deltaMs} ms não divide a hora");
        Record(world, actor, PlayerCommandKind.Advance, null,
            new PlayerCommandRecord(0, 0, actor, PlayerCommandKind.Advance, DeltaMs: deltaMs, Ticks: ticks));
        clock.Run(world, ticks, deltaMs);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>A hora do dia <paramref name="hourOfDay"/> que vem depois de agora, em ms (amanhã se já passou ou é agora).</summary>
    public static long NextHourOfDayMs(WorldState world, int hourOfDay)
    {
        var date = world.CurrentDate;
        int perDay = date.Calendar.HoursPerDay;
        int ahead = ((hourOfDay - date.Hour) % perDay + perDay) % perDay;
        if (ahead == 0) ahead = perDay;
        return WorldMillis.FromHours(date.TotalHours + ahead);
    }

    /// <summary>Encerra o pulo: quem dormia acorda (fica à toa, no controle do jogador). Vai para o registro com o motivo.</summary>
    public Result<Unit> EndSkip(WorldState world, string reason)
    {
        if (Skip is not { } skip) return Result<Unit>.Fail("wake: não há pulo de tempo em curso");
        Skip = null;
        LastWakeReason = reason;
        var npc = world.FindNpc(new NpcId(skip.NpcId));
        if (skip.Sleeping && npc is { IsAlive: true } && npc.ControllerId == skip.PlayerId)
            PlayerCommands.Act(world, npc.Id, skip.PlayerId, ActionType.Idle);
        Record(world, skip.PlayerId, PlayerCommandKind.Wake, skip.NpcId,
            new PlayerCommandRecord(0, 0, skip.PlayerId, PlayerCommandKind.Wake, Reason: reason));
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>O hospedeiro pergunta a cada ciclo se o pulo tem de acabar agora (e por quê): o que acorda nasce do estado do motor
    /// (<see cref="SkipWatch"/>), outro jogador chegando, o horário pedido ou o descanso completo.</summary>
    public string? SkipEnding(WorldState world)
    {
        if (Skip is not { } skip) return null;
        if (world.FindNpc(new NpcId(skip.NpcId)) is not { IsAlive: true } npc || npc.ControllerId != skip.PlayerId) return "fim";
        if (PlayersPresent(world) > 1) return "outro jogador";
        if (SkipWatch.Interruption(world, npc, skip) is { } reason) return reason;
        long nowMs = world.CurrentDate.TotalMilliseconds;
        if (skip.Task) return PlayerTasks.Of(world, npc) is null ? "feito" : null; // acabou, ou o jogador a interrompeu por outro caminho
        if (skip.UntilMs is { } until && nowMs >= until) return "hora";
        if (skip.Sleeping && skip.UntilMs is null && npc.CurrentAction == ActionType.Sleep
            && npc.SleepAt(world.CurrentDate.TotalHours) >= SkipWatch.Rested) return "descansado";
        return null;
    }

    private Result<Unit>? Refusal(WorldState world, string playerId)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is null)
            return Result<Unit>.Fail($"{PlayerCommands.NotFound} jogador não controla nenhum NPC");
        if (!CanSkip(world, playerId)) return Result<Unit>.Fail($"{PlayerCommands.Conflict} só quem joga sozinho pula o tempo");
        if (Skip is not null) return Result<Unit>.Fail($"{PlayerCommands.Conflict} já há um pulo de tempo em curso");
        return null;
    }

    private void Begin(WorldState world, Npc npc, string playerId, bool sleeping, long? untilMs)
    {
        Skip = new TimeSkip(playerId, npc.Id.Value, sleeping, untilMs, world.CurrentDate.TotalHours, npc.Health);
        LastWakeReason = null;
    }

    /// <summary>#241: com o corpo ocupado numa ação longa (cortar lenha leva uma jornada) e o jogador podendo pular o tempo (sozinho no
    /// mundo), o mundo acelera até ela acabar: a mesma sequência de ticks do salto do sono, só mais rápida, com as mesmas interrupções
    /// (<see cref="SkipEnding"/>). Ação curta (menos de uma hora de mundo, o Δ do salto) corre em tempo normal. Com mais de um jogador
    /// não há salto e a ação corre no ritmo normal.</summary>
    internal void BeginTaskSkip(WorldState world, Npc npc, string playerId)
    {
        if (Skip is not null || !CanSkip(world, playerId) || PlayerTasks.Of(world, npc) is not { } busy || busy.RemainingMs < WorldMillis.HourTick) return;
        Skip = new TimeSkip(playerId, npc.Id.Value, false, busy.UntilMs, world.CurrentDate.TotalHours, npc.Health, Task: true);
        LastWakeReason = null;
    }

    private void EndSkipOnCommand(WorldState world, string playerId)
    {
        // #241: o salto de uma ação longa não acaba com qualquer comando (o cliente sincroniza a pose, o passo é recusado ocupado): acaba
        // com a ação, com os eventos do motor, com Interromper (que o solta de propósito) ou com Parar de acelerar
        if (Skip is { Task: false } skip && skip.PlayerId == playerId) EndSkip(world, "comando");
    }
}

/// <summary>O que acorda quem pula o tempo (AD-197c), lido do estado real do motor, sem regra no cliente: golpe (o corpo ferido e a
/// mágoa de agressão desde o começo do pulo, o rastro de <c>Violence.Fight</c>), fogo (o calor do lugar: o motor ainda não tem
/// incêndio, só clima e poder de temperatura), fome ou sede críticas e alguém da casa ou próximo conversando ao lado (chamando).</summary>
public static class SkipWatch
{
    /// <summary>Sono a partir do qual quem dorme acelerado acorda sozinho (descansado).</summary>
    public const int Rested = 95;

    public static string? Interruption(WorldState world, Npc npc, TimeSkip skip)
    {
        var rules = world.Players.Time;
        long now = world.CurrentDate.TotalHours;
        if (npc.Health < skip.HealthAtStart && Struck(world, npc, skip.StartedTick)) return "golpe";
        if (LivingWorld.Simulation.Extraordinary.Mechanics.EnvironmentTemperatureMechanic.EffectiveTemperature(world, npc.CurrentLocation, now)
            >= rules.BurnTemperatureC) return "fogo";
        if (100 - npc.HungerAt(now) >= rules.CriticalNeedDeficit) return "fome";
        if (100 - npc.ThirstAt(now) >= rules.CriticalNeedDeficit) return "sede";
        if (Caller(world, npc, rules.CallerAffection) is not null) return "chamado";
        return null;
    }

    private static bool Struck(WorldState world, Npc npc, long sinceTick)
    {
        if (world.Crime is not { } ledger) return false;
        foreach (var grudge in ledger.Grudges)
            if (grudge.Holder == npc.Id.Value && grudge.Motive == "agressao" && grudge.Tick >= sinceTick) return true;
        return false;
    }

    /// <summary>Quem mora com a pessoa, ou alguém que ela conhece bem (afeição ≥ <paramref name="affection"/>), acordado e conversando
    /// na mesma célula ou ao lado (o de menor id).</summary>
    public static Npc? Caller(WorldState world, Npc npc, double affection)
    {
        Npc? found = null;
        void Consider(NpcId id)
        {
            if (id == npc.Id || world.FindNpc(id) is not { IsAlive: true } other || other.CurrentAction != ActionType.Socialize) return;
            var at = other.CurrentLocation;
            if (Math.Max(Math.Abs(at.X - npc.CurrentLocation.X), Math.Abs(at.Y - npc.CurrentLocation.Y)) > 1) return;
            if (found is null || other.Id.Value < found.Id.Value) found = other;
        }
        if (npc.Household is { } home && world.FindHousehold(home) is { } household)
            foreach (var member in household.Members) Consider(member);
        var targets = world.RelationshipTargetsOf(npc.Id);
        if (world.RelationshipsFrom(npc.Id) is { } bonds)
            for (int i = 0; i < targets.Count; i++)
                if (bonds[i].Affection >= affection) Consider(targets[i]);
        return found;
    }
}
