using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Players;

/// <summary>#267: "ir até" (como o Sims: clicar numa pessoa, num móvel ou num prédio leva o corpo até lá). O corpo conduzido anda sozinho, por tick fino, pela MESMA rota do andar até o
/// móvel e da interação social de longe (<see cref="PlayerWalk.Approach"/>: busca pelas portas, nunca atravessa parede, gente na frente barra): o alvo é uma célula, uma pessoa (a rota segue
/// onde ela está agora) ou a porta de um prédio (a soleira). Chegando (ou chegando ao alcance de uma pessoa), o corpo para; WASD, Espaço ou outra ordem interrompem (o corpo fica ocupado como
/// em qualquer ação longa). Nada escrito em dado de cenário: a velocidade é a do corpo; só o rótulo que o cliente lê vem no pedido.</summary>
public static class PlayerGo
{
    public const string Action = "go";
    private const long MinuteMs = 60_000;
    private const long GiveUpMs = 6_000; // barrado (sem rota ou gente na frente) por este tempo: desiste
    private const double ReachM = 1.0; // chegando a pessoa: a esta distância o corpo para

    /// <summary>O alvo resolvido: a célula do mundo e, se é uma pessoa, o id dela (o corpo segue onde ela estiver).</summary>
    public readonly record struct Target(CellCoord Cell, long? Person, long EnterBuilding = 0);

    /// <summary>Resolve o pedido (célula, pessoa ou prédio) para o alvo; o motivo da recusa em <c>Error</c>.</summary>
    public static Result<Target> Resolve(WorldState world, Npc npc, int? x, int? y, long? person, long? building)
    {
        if (person is { } who)
        {
            if (world.FindNpc(new NpcId(who)) is not { IsAlive: true } other || other.Id == npc.Id) return Result<Target>.Fail($"{PlayerCommands.NotFound} go: pessoa ausente");
            if (LivingWorld.Simulation.Careers.CareerWork.IsAway(world, other)) return Result<Target>.Fail($"{PlayerCommands.Conflict} go: {other.Name} está no trabalho");
            if (PlayerGround.FloorOf(other) != PlayerGround.FloorOf(npc)) return Result<Target>.Fail($"{PlayerCommands.Conflict} go: {other.Name} está em outro andar");
            return Result<Target>.Ok(new Target(PlayerGround.PoseOf(world, other), who));
        }

        if (building is { } id)
        {
            if (world.FindBuilding(new BuildingId(id)) is not { } place || PlayerGround.DoorOf(place) is not { } door) return Result<Target>.Fail($"{PlayerCommands.NotFound} go: prédio ausente");
            return Result<Target>.Ok(new Target(door.Doorstep, null));
        }

        if (x is not { } cx || y is not { } cy) return Result<Target>.Fail("go: informe a célula, a pessoa ou o prédio");
        var cell = new CellCoord(cx, cy);
        return world.Map.TryGetCell(cell, out _) ? Result<Target>.Ok(new Target(cell, null)) : Result<Target>.Fail("go: fora do mapa");
    }

    /// <summary>Põe o corpo a caminho. Recusa (409) com o corpo ocupado, em outro andar ou sem passo fino; o mundo não muda.</summary>
    public static Result<Unit> Begin(WorldState world, TickContext ctx, Npc npc, Target target, string? label)
    {
        long now = world.CurrentDate.TotalMilliseconds;
        if (!world.FineTime) return Result<Unit>.Fail($"{PlayerCommands.Conflict} go: só no passo fino");
        if (PlayerTasks.Of(world, npc) is not null || now < npc.StepReadyAtMs) return Result<Unit>.Fail($"{PlayerCommands.Conflict} go: o corpo está ocupado");
        if (PlayerGround.FloorOf(npc) > 0) return Result<Unit>.Fail($"{PlayerCommands.Conflict} go: use a escada primeiro");
        var from = PlayerGround.PoseOf(world, npc);
        long cells = Math.Max(Math.Abs(from.X - target.Cell.X), Math.Abs(from.Y - target.Cell.Y));
        double mps = Math.Max(0.3, world.Players.WalkMps);
        long estimate = Math.Max(4000, (long)(cells * 1.4 * world.Players.CellM / mps * 1000) * 2 + 3000);
        PlayerCommands.TakeTheWheel(world, npc);
        var task = new BodyTask(Action, now, now + estimate)
        {
            CellX = target.Cell.X,
            CellY = target.Cell.Y,
            Use = Clean(label),
            LastMs = now,
            Units = target.Person is { } p ? p + 1 : 0,
            ThenEnter = target.EnterBuilding,
        };
        npc.SetBodyTask(task);
        ctx.ScheduleEventAtMs(now + 1, BodyTaskSystem.SystemName, Payload(npc, now));
        return Result<Unit>.Ok(Unit.Value);
    }

    private static string? Clean(string? label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim().Length > 40 ? label.Trim()[..40] : label.Trim();

    internal static string Payload(Npc npc, long startedAtMs) => $"{npc.Id.Value}|{startedAtMs}|g";

    /// <summary>O evento do passo: anda o trecho decorrido por <see cref="PlayerWalk.Approach"/>; chega, desiste (barrado) ou agenda o próximo tick. Evento de tarefa que já acabou é velho e sai.</summary>
    public static void Step(WorldState world, TickContext ctx, Npc npc, long startedAtMs)
    {
        if (npc.BodyTask is not { Action: Action } task || task.StartedAtMs != startedAtMs) return;
        long now = world.CurrentDate.TotalMilliseconds;
        if (npc.ControllerId is null) { Finish(world, npc, now); return; }
        long goalX = task.CellX * (long)FinePose.PerCell + FinePose.PerCell / 2, goalY = task.CellY * (long)FinePose.PerCell + FinePose.PerCell / 2;
        bool person = task.Units > 0;
        if (person)
        {
            if (world.FindNpc(new NpcId(task.Units - 1)) is not { IsAlive: true } other || LivingWorld.Simulation.Careers.CareerWork.IsAway(world, other)) { Finish(world, npc, now); return; }
            var cell = PlayerGround.PoseOf(world, other);
            (goalX, goalY) = other.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y ? (f.X, f.Y) : (cell.X * (long)FinePose.PerCell + FinePose.PerCell / 2, cell.Y * (long)FinePose.PerCell + FinePose.PerCell / 2);
            task = task with { CellX = cell.X, CellY = cell.Y };
        }

        var here = PlayerGround.PoseOf(world, npc);
        var pose = npc.Fine is { } own && own.CellX == here.X && own.CellY == here.Y ? own : new FinePose(here.X * FinePose.PerCell + 500, here.Y * FinePose.PerCell + 500, 0, 0, now);
        long elapsed = Math.Clamp(now - Math.Max(task.LastMs, pose.AtMs), 0, 4L * Math.Max(1, world.TickDeltaMs));
        var moved = PlayerWalk.Approach(world, ctx, npc, pose, goalX, goalY, elapsed);
        double reach = ReachM / Math.Max(0.1, world.Players.CellM) * FinePose.PerCell;
        double gx = moved.Pose.X - goalX, gy = moved.Pose.Y - goalY, gap = Math.Sqrt(gx * gx + gy * gy);
        if (moved.Arrived || (person && gap <= reach))
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Finish(world, npc, now);
            if (!person && moved.Arrived && world.Careers is not null) LivingWorld.Simulation.Careers.CareerWork.OnArrived(world, ctx, npc, new CellCoord(task.CellX, task.CellY)); // #268: a candidatura que esperava a chegada
            if (!person && moved.Arrived && (task.ThenEnter > 0 ? new BuildingId(task.ThenEnter - 1) : OwnDoorBuilding(world, npc, new CellCoord(task.CellX, task.CellY))) is { } enter) EnterDoor(world, ctx, npc, enter); // #269: a porta da própria casa leva para dentro
            return;
        }

        npc.SetFine(moved.Pose with { AtMs = now });
        if (moved.Blocked && now - task.StartedAtMs > GiveUpMs)
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Finish(world, npc, now);
            return;
        }

        npc.SetBodyTask(task with { LastMs = now, UntilMs = Math.Max(task.UntilMs, now + 4000) });
        ctx.ScheduleEventAtMs(now + Math.Max(1, world.TickDeltaMs), BodyTaskSystem.SystemName, Payload(npc, task.StartedAtMs));
    }

    /// <summary>#269: o prédio da própria casa se <paramref name="cell"/> é a soleira dele (chegar ali por um clique na casa é entrar: função do alvo, igual no registro); nulo senão.</summary>
    private static BuildingId? OwnDoorBuilding(WorldState world, Npc npc, CellCoord cell) =>
        npc.Household is { } h && world.FindHousehold(h)?.Dwelling is { } dwelling && world.FindBuilding(dwelling) is { } home && PlayerGround.DoorOf(home) is { } door && door.Doorstep == cell ? home.Id : null;

    /// <summary>#269: o passo da soleira para dentro (o mesmo da roda do E: <see cref="PlayerGround.Step"/> valida a parede e a gente na porta); o que recusa deixa o corpo na soleira.</summary>
    internal static Result<Unit> EnterDoor(WorldState world, TickContext ctx, Npc npc, BuildingId building)
    {
        if (world.FindBuilding(building) is not { } place || PlayerGround.DoorOf(place) is not { } door) return Result<Unit>.Fail($"{PlayerCommands.NotFound} go: prédio ausente");
        var from = PlayerGround.PoseOf(world, npc);
        if (from != door.Doorstep) return Result<Unit>.Fail($"{PlayerCommands.Conflict} go: longe da porta");
        var step = PlayerGround.Step(world, npc, from, door.Door);
        return step.IsSuccess ? PlayerCommands.Walk(world, ctx, npc, from, door.Door) : step;
    }

    private static void Finish(WorldState world, Npc npc, long now)
    {
        npc.SetBodyTask(null);
        npc.Step(now, 0, now);
    }

    /// <summary>Para o corpo onde está (WASD, Espaço): sem rota guardada.</summary>
    public static void Stop(WorldState world, Npc npc) => Finish(world, npc, world.CurrentDate.TotalMilliseconds);

    /// <summary>O que o cliente lê em <c>busy</c>: rótulo, fase <c>walk</c> e o ponto onde o corpo chegará (em células).</summary>
    public static (string Label, double Fx, double Fy)? View(BodyTask task) =>
        task.Action == Action ? (task.Use ?? "", task.CellX + 0.5, task.CellY + 0.5) : null;
}
