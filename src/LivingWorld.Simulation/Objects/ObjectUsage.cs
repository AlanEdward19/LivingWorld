using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Objects;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Players;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Objects;

/// <summary>Uma peça e o uso dela como o motor os resolveu: o prédio, a peça, o uso do catálogo e o lugar onde o corpo fica (milésimos de célula) e para onde olha.</summary>
public sealed record UseTarget(Building Building, int Piece, ObjectDescriptor Descriptor, ObjectUseDef Use, CellCoord Cell, int Floor, long FineX, long FineY, double Facing);

/// <summary>Uma ação da fila: o id do uso, o nome dele ("Tomar banho") e o do objeto ("Chuveiro").</summary>
public sealed record QueueView(string Use, string Label, string Object);

/// <summary>#255 (S2 do sandbox): usar um objeto da casa, tudo pelo catálogo (<see cref="ObjectUseDef"/>). O corpo, de jogador ou de NPC, anda até o lugar
/// de uso da peça (o motor leva, pelo mesmo chão do passo), vira para ela e só então a ação começa; ela dura o que o catálogo diz, a necessidade sobe
/// durante (em fatias, não de uma vez) e interromper mantém o que já subiu. Ocupa o corpo como a lenha (<see cref="BodyTask"/>), com fila de até
/// <see cref="Npc.MaxUseQueue"/> ações. Tudo determinístico: eventos agendados no tempo de mundo (<see cref="BodyTaskSystem"/>), sem tick por pessoa.</summary>
public static class ObjectUsage
{
    /// <summary>O <c>Action</c> do <see cref="BodyTask"/> de um uso de objeto.</summary>
    public const string Action = "use";

    private const long MinuteMs = 60_000;
    private const long SliceMinMs = 15_000;
    private const long SliceMaxMs = 5 * MinuteMs;

    /// <summary>Quanto o corpo espera por uma passagem que não abre (gente na frente) antes de desistir do uso.</summary>
    private const long GiveUpMs = 2 * MinuteMs;

    public static bool Active(Npc npc) => npc.BodyTask is { Action: Action };

    /// <summary>O uso que já passou do fim mas cujo evento ainda não correu (o tick chegou antes dele): assenta agora, para nada ser perdido nem sobrescrito.</summary>
    public static void SettleDue(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.BodyTask is { Action: Action } task && world.CurrentDate.TotalMilliseconds >= task.UntilMs && task.Doing) Step(world, ctx, npc, task.StartedAtMs);
    }

    /// <summary>A peça e o uso resolvidos (ou o motivo da recusa). Não olha quem usa.</summary>
    public static Result<UseTarget> Resolve(WorldState world, long buildingId, int piece, string useId)
    {
        if (world.ObjectCatalog is not { } catalog || world.FindBuilding(new BuildingId(buildingId)) is not { Position: { } origin } building)
            return Result<UseTarget>.Fail($"{PlayerCommands.NotFound} o prédio não tem móveis");
        var pieces = HomeFurniture.Of(building, catalog);
        if (piece < 0 || piece >= pieces.Count || pieces[piece].Spot is not { } spot)
            return Result<UseTarget>.Fail($"{PlayerCommands.NotFound} a peça não está na casa");
        if (catalog.Find(pieces[piece].Id) is not { } descriptor || descriptor.Uses?.FirstOrDefault(u => u.Id == useId) is not { } use)
            return Result<UseTarget>.Fail($"{PlayerCommands.NotFound} esse objeto não tem esse uso");
        var cell = new CellCoord(origin.X + spot.Cell.X, origin.Y + spot.Cell.Y);
        double turns = spot.Facing / 360.0, cos = DeterministicMath.CosTurns(turns), sin = DeterministicMath.SinTurns(turns);
        double sx = use.Slot?[0] ?? 0.0, sz = use.Slot?[1] ?? 0.0;
        double cellM = Math.Max(0.1, world.Players.CellM);
        long fx = cell.X * FinePose.PerCell + 500 + (long)Math.Round((sx * cos + sz * sin) / cellM * FinePose.PerCell);
        long fy = cell.Y * FinePose.PerCell + 500 + (long)Math.Round((-sx * sin + sz * cos) / cellM * FinePose.PerCell);
        fx = Math.Clamp(fx, cell.X * FinePose.PerCell + 40, (cell.X + 1) * FinePose.PerCell - 40);
        fy = Math.Clamp(fy, cell.Y * FinePose.PerCell + 40, (cell.Y + 1) * FinePose.PerCell - 40);
        return Result<UseTarget>.Ok(new UseTarget(building, piece, descriptor, use, cell, spot.Floor, fx, fy, spot.Facing + use.Facing));
    }

    /// <summary>Quem está com a peça (indo até ela ou usando) e ocupa o uso, fora <paramref name="except"/>.</summary>
    public static Npc? OccupiedBy(WorldState world, long buildingId, int piece, Npc except)
    {
        foreach (var other in world.AliveNpcIndex.Alive)
            if (other.Id != except.Id && other.BodyTask is { Action: Action, Use: { } use } task && task.Building == buildingId && task.Piece == piece
                && Resolve(world, buildingId, piece, use) is { IsSuccess: true, Value.Use.Occupies: true })
                return other;
        return null;
    }

    /// <summary>A despensa da casa tem o que o uso gasta? Texto da falta, ou nulo.</summary>
    public static string? Shortage(WorldState world, Npc npc, ObjectUseDef use)
    {
        if (use.Consumes is not { Count: > 0 } consumes) return null;
        var household = npc.Household is { } h ? world.FindHousehold(h) : null;
        foreach (var c in consumes)
            if (household is null || household.Stock.GetValueOrDefault(new ResourceType(c.Resource)) < c.Quantity)
                return "falta o que gastar na despensa";
        return null;
    }

    /// <summary>Começa o uso: valida (alcance, andar, ocupado, despensa) e põe o corpo a caminho do lugar de uso. O NPC que não é conduzido chega na hora (a casa é
    /// pequena e o motor o põe no lugar); o corpo do jogador anda de verdade, a cada tick fino, e a ação só começa ao chegar.</summary>
    public static Result<Unit> Begin(WorldState world, TickContext ctx, Npc npc, long buildingId, int piece, string useId)
    {
        var resolved = Resolve(world, buildingId, piece, useId);
        if (!resolved.IsSuccess) return Result<Unit>.Fail(resolved.Error!);
        var target = resolved.Value!;
        SettleDue(world, ctx, npc);
        long now = world.CurrentDate.TotalMilliseconds;
        if (PlayerTasks.Of(world, npc) is not null || now < npc.StepReadyAtMs) return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: o corpo está ocupado");
        if (PlayerGround.FloorOf(npc) != target.Floor && npc.ControllerId is not null) return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: está em outro andar");
        if (target.Use.Occupies && OccupiedBy(world, buildingId, piece, npc) is { } who) return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: {who.Name} está usando");
        if (Shortage(world, npc, target.Use) is { } missing) return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: {missing}");
        if (LivingWorld.Simulation.Careers.CareerWork.BlockedUse(world, npc, target.Use) is { } cut) return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: {cut}"); // #263: serviço cortado

        var task = new BodyTask(Action, now, now + 10 * MinuteMs) { Use = useId, Building = buildingId, Piece = piece, LastMs = now };
        if (npc.ControllerId is null || !world.FineTime)
        {
            Place(world, npc, target);
            npc.SetBodyTask(task);
            return Arrive(world, ctx, npc, target, now);
        }

        PlayerCommands.TakeTheWheel(world, npc);
        npc.SetBodyTask(task with { UntilMs = now + EstimateApproachMs(world, npc, target) });
        ctx.ScheduleEventAtMs(now + 1, BodyTaskSystem.SystemName, Payload(npc, now));
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Estimativa (com folga) do tempo de ir até o lugar, para a barra; a chegada real é a do motor.</summary>
    private static long EstimateApproachMs(WorldState world, Npc npc, UseTarget target)
    {
        var from = PlayerGround.PoseOf(world, npc);
        long cells = Math.Max(Math.Abs(from.X - target.Cell.X), Math.Abs(from.Y - target.Cell.Y));
        double mps = Math.Max(0.3, world.Players.WalkMps);
        long ms = (long)(cells * 1.4 * world.Players.CellM / mps * 1000);
        return Math.Max(4000, ms * 2 + 3000);
    }

    internal static string Payload(Npc npc, long startedAtMs) => $"{npc.Id.Value}|{startedAtMs}|u";

    /// <summary>Põe o corpo do NPC (não conduzido) no lugar de uso, dentro do prédio.</summary>
    private static void Place(WorldState world, Npc npc, UseTarget target)
    {
        var local = new CellCoord(target.Cell.X - target.Building.Position!.Value.X, target.Cell.Y - target.Building.Position!.Value.Y);
        if (npc.ControllerId is not null)
        {
            npc.SetFine(new FinePose(target.FineX, target.FineY, 0, 0, world.CurrentDate.TotalMilliseconds));
            if (PlayerGround.PoseOf(world, npc) != target.Cell) npc.MoveTo(target.Cell, world.CurrentDate.TotalHours);
        }
        else if (npc.Interior is { } inside && inside.Building == target.Building.Id) npc.MoveWithinBuilding(local);
        else npc.EnterBuilding(target.Building.Id, new FloorLevel(target.Floor), local);
    }

    /// <summary>O evento do uso (<see cref="BodyTaskSystem"/>): a caminhada a cada tick fino ou a fatia de efeito. Evento de uma tarefa que já acabou é velho e sai.</summary>
    public static void Step(WorldState world, TickContext ctx, Npc npc, long startedAtMs)
    {
        if (npc.BodyTask is not { Action: Action, Use: { } useId } task || task.StartedAtMs != startedAtMs) return;
        long now = world.CurrentDate.TotalMilliseconds;
        var resolved = Resolve(world, task.Building, task.Piece, useId);
        if (!resolved.IsSuccess) { Finish(world, ctx, npc, now, startNext: false); return; }
        var target = resolved.Value!;
        if (!task.Doing) Walk(world, ctx, npc, task, target, now);
        else Slice(world, ctx, npc, task, target, now);
    }

    private static void Walk(WorldState world, TickContext ctx, Npc npc, BodyTask task, UseTarget target, long now)
    {
        if (!world.FineTime)
        {
            Place(world, npc, target);
            Arrive(world, ctx, npc, target, now);
            return;
        }

        var cell = PlayerGround.PoseOf(world, npc);
        var pose = npc.Fine is { } f && f.CellX == cell.X && f.CellY == cell.Y ? f : new FinePose(cell.X * FinePose.PerCell + 500, cell.Y * FinePose.PerCell + 500, 0, 0, now);
        long elapsed = Math.Clamp(now - Math.Max(task.LastMs, pose.AtMs), 0, 4L * Math.Max(1, world.TickDeltaMs));
        var moved = PlayerWalk.Approach(world, ctx, npc, pose, target.FineX, target.FineY, elapsed);
        if (moved.Arrived)
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Arrive(world, ctx, npc, target, now);
            return;
        }

        npc.SetFine(moved.Pose with { AtMs = now });
        if (moved.Blocked && now - task.StartedAtMs > GiveUpMs)
        {
            npc.SetFine(moved.Pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
            Finish(world, ctx, npc, now, startNext: true);
            return;
        }

        npc.SetBodyTask(task with { LastMs = now, UntilMs = Math.Max(task.UntilMs, now + 4000) });
        ctx.ScheduleEventAtMs(now + Math.Max(1, world.TickDeltaMs), BodyTaskSystem.SystemName, Payload(npc, task.StartedAtMs));
    }

    /// <summary>O corpo chegou: gasta o que o uso gasta e começa a ação (a do catálogo, ou a do motor quando o uso delega: dormir, comer à mesa).</summary>
    private static Result<Unit> Arrive(WorldState world, TickContext ctx, Npc npc, UseTarget target, long now)
    {
        var use = target.Use;
        if (Shortage(world, npc, use) is not null) { Finish(world, ctx, npc, now, startNext: true); return Result<Unit>.Fail($"{PlayerCommands.Conflict} use: falta o que gastar"); }
        if (use.Consumes is { } consumes && npc.Household is { } h && world.FindHousehold(h) is { } household)
            foreach (var c in consumes)
            {
                household.Withdraw(new ResourceType(c.Resource), c.Quantity);
                if (use.Produces is not null) world.RecordResourceConsumed(new ResourceType(c.Resource), c.Quantity); // #258: o que o fogão transforma sai da conta da comida
            }
        if (use.Toggle is { } toggle && toggle != ObjectToggles.JobSearch)
        {
            // #258: ligar ou desligar a luz do objeto: o estado é do motor, a ação é instantânea (o corpo já está ao lado da peça)
            world.SetLight(target.Building.Id.Value, target.Piece, toggle == ObjectToggles.LightOn);
            Finish(world, ctx, npc, now, startNext: true);
            return Result<Unit>.Ok(Unit.Value);
        }

        if (use.Action is { } action)
        {
            // dormir, comer à mesa: o corpo está no lugar; a ação do motor assume (a mesma de sempre, com a duração e o efeito dela)
            npc.SetBodyTask(null);
            npc.Step(now, 0, now);
            if (npc.ControllerId is { } player) PlayerCommands.Act(world, npc.Id, player, (ActionType)action);
            return Result<Unit>.Ok(Unit.Value);
        }

        long duration = DurationMs(world, npc, use, now);
        long until = now + duration;
        npc.Step(until, 0, now);
        npc.SetBodyTask(new BodyTask(Action, now, until) { Use = use.Id, Building = target.Building.Id.Value, Piece = target.Piece, Doing = true, LastMs = now });
        ScheduleSlice(ctx, npc, now, until, duration);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>#261: alguma necessidade do catálogo está na metade do limite de urgência ou abaixo: atende-la não espera a fome nem a sede (quem tem fome mas vai à bexiga a zero).</summary>
    public static bool HasCriticalNeed(WorldState world, Npc npc, long nowMs)
    {
        if (npc.ExtraNeeds is null || HomeLife.Catalog(world) is not { } needs) return false;
        foreach (var d in needs.Needs)
            if ((HomeLife.Value(world, npc, d.Id, nowMs) ?? d.Start) < d.UrgentBelow / 2.0) return true;
        return false;
    }

    /// <summary>#261: os minutos de mundo que o uso levaria para esta pessoa agora (o rótulo da roda bate com o relógio: com <c>untilFull</c> é o que falta para encher, não o teto do catálogo).</summary>
    public static int PlannedMinutes(WorldState world, Npc npc, ObjectUseDef use) =>
        (int)Math.Max(1, (DurationMs(world, npc, use, world.CurrentDate.TotalMilliseconds) + MinuteMs - 1) / MinuteMs);

    /// <summary>Quanto o uso dura: o do catálogo, ou — com <c>untilFull</c> — o que a necessidade leva para encher (no máximo o do catálogo, no mínimo um minuto).</summary>
    private static long DurationMs(WorldState world, Npc npc, ObjectUseDef use, long now)
    {
        long max = use.DurationMin * MinuteMs;
        if (use.UntilFull is not { } need || HomeLife.Value(world, npc, need, now) is not { } value) return max;
        double rate = use.Effects.Where(e => e.Need == need).Sum(e => e.PerHour);
        if (rate <= 0) return max;
        return Math.Clamp((long)Math.Ceiling((100.0 - value) / rate * WorldMillis.PerHour), MinuteMs, max);
    }

    private static void ScheduleSlice(TickContext ctx, Npc npc, long now, long until, long duration)
    {
        long slice = Math.Clamp(duration / 12, SliceMinMs, SliceMaxMs);
        ctx.ScheduleEventAtMs(Math.Min(until, now + slice), BodyTaskSystem.SystemName, Payload(npc, now));
    }

    /// <summary>Aplica os efeitos do trecho já vivido (de <c>LastMs</c> até agora ou até o fim) e agenda a próxima fatia; no fim, libera o corpo e começa a próxima da fila.</summary>
    private static void Slice(WorldState world, TickContext ctx, Npc npc, BodyTask task, UseTarget target, long now)
    {
        long to = Math.Min(now, task.UntilMs);
        Apply(world, npc, target.Use, task.LastMs, to);
        if (now >= task.UntilMs)
        {
            Produce(world, npc, target.Use); // #258
            if (target.Use.Toggle == ObjectToggles.JobSearch) LivingWorld.Simulation.Careers.CareerWork.Search(world, npc, now); // #263: o jornal lista as vagas ao terminar: o preparo terminou de verdade: o que o fogão fez chega à despensa
            Finish(world, ctx, npc, now, startNext: true);
            return;
        }

        npc.SetBodyTask(task with { LastMs = to });
        ctx.ScheduleEventAtMs(Math.Min(task.UntilMs, now + Math.Clamp((task.UntilMs - task.StartedAtMs) / 12, SliceMinMs, SliceMaxMs)),
            BodyTaskSystem.SystemName, Payload(npc, task.StartedAtMs));
    }

    /// <summary>#258: põe na despensa da casa o que o uso produz (a refeição que saiu do fogão). Só no fim natural: interromper o preparo o perde.</summary>
    private static void Produce(WorldState world, Npc npc, ObjectUseDef use)
    {
        if (use.Produces is not { Count: > 0 } produces || npc.Household is not { } h || world.FindHousehold(h) is not { } household) return;
        foreach (var p in produces)
        {
            household.Deposit(new ResourceType(p.Resource), p.Quantity);
            world.RecordResourceProduced(new ResourceType(p.Resource), p.Quantity);
        }
    }

    private static void Apply(WorldState world, Npc npc, ObjectUseDef use, long fromMs, long toMs)
    {
        if (toMs <= fromMs) return;
        double hours = (toMs - fromMs) / (double)WorldMillis.PerHour;
        foreach (var effect in use.Effects)
            HomeLife.Add(world, npc, effect.Need, effect.PerHour * hours, toMs);
        if (world.Careers is not null) LivingWorld.Simulation.Careers.CareerWork.Meter(world, npc, use, hours); // #263: o uso mede a conta de água
    }

    /// <summary>Libera o corpo e, se foi o fim natural (ou a queda do uso), começa a próxima ação da fila que ainda vale.</summary>
    private static void Finish(WorldState world, TickContext ctx, Npc npc, long now, bool startNext)
    {
        npc.SetBodyTask(null);
        npc.Step(now, 0, now);
        if (npc.ControllerId is null) PlayerCommands.WakeNow(world, npc);
        if (startNext) StartNext(world, ctx, npc);
    }

    private static void StartNext(WorldState world, TickContext ctx, Npc npc)
    {
        while (npc.UseQueue is { Count: > 0 } queue)
        {
            var next = queue[0];
            npc.SetUseQueue(queue.Skip(1).ToList());
            if (npc.ControllerId is not null && Begin(world, ctx, npc, next.Building, next.Piece, next.Use).IsSuccess) return;
        }
    }

    /// <summary>Para o uso agora (o jogador mexeu, apertou Espaço): o que já subiu fica, o resto não existiu, e a fila é descartada (o jogador tomou o corpo).
    /// Devolve o que estava em curso, ou nulo se não havia uso.</summary>
    public static BodyTask? Stop(WorldState world, TickContext ctx, Npc npc)
    {
        if (npc.BodyTask is not { Action: Action, Use: { } useId } task) return null;
        long now = world.CurrentDate.TotalMilliseconds;
        if (task.Doing && Resolve(world, task.Building, task.Piece, useId) is { IsSuccess: true } resolved)
            Apply(world, npc, resolved.Value!.Use, task.LastMs, Math.Min(now, task.UntilMs));
        npc.SetUseQueue(null);
        Finish(world, ctx, npc, now, startNext: false);
        if (npc.Fine is { } pose) npc.SetFine(pose with { HeadingX = 0, HeadingY = 0, AtMs = now });
        return task;
    }

    /// <summary>Põe o uso no fim da fila (até <see cref="Npc.MaxUseQueue"/>); com o corpo livre e a fila vazia, começa já.</summary>
    public static Result<Unit> Enqueue(WorldState world, TickContext ctx, Npc npc, long buildingId, int piece, string useId)
    {
        var resolved = Resolve(world, buildingId, piece, useId);
        if (!resolved.IsSuccess) return Result<Unit>.Fail(resolved.Error!);
        long now = world.CurrentDate.TotalMilliseconds;
        bool free = PlayerTasks.Of(world, npc) is null && now >= npc.StepReadyAtMs && npc.UseQueue is not { Count: > 0 };
        if (free) return Begin(world, ctx, npc, buildingId, piece, useId);
        var queue = npc.UseQueue?.ToList() ?? [];
        if (queue.Count >= Npc.MaxUseQueue) return Result<Unit>.Fail($"{PlayerCommands.Conflict} fila: no máximo {Npc.MaxUseQueue} ações");
        queue.Add(new QueuedUse(buildingId, piece, useId));
        npc.SetUseQueue(queue);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>Tira a ação <paramref name="index"/> (0 = a próxima) da fila.</summary>
    public static Result<Unit> Dequeue(Npc npc, int index)
    {
        var queue = npc.UseQueue?.ToList() ?? [];
        if (index < 0 || index >= queue.Count) return Result<Unit>.Fail($"{PlayerCommands.NotFound} fila: não há essa ação");
        queue.RemoveAt(index);
        npc.SetUseQueue(queue);
        return Result<Unit>.Ok(Unit.Value);
    }

    /// <summary>A pessoa que ninguém conduz cuida de si: com a necessidade do catálogo abaixo do limite (mais <paramref name="margin"/>: quem vai dormir passa no vaso
    /// antes), em casa e à toa, escolhe a necessidade mais urgente e, para ela, o uso que mais a sobe entre os objetos livres da casa, e vai usá-lo. Mesmo catálogo e
    /// mesma regra do jogador. Verdadeiro se começou um uso.</summary>
    public static bool TryCare(WorldState world, TickContext ctx, Npc npc, double margin = 0)
    {
        if (npc.ControllerId is not null || npc.ExtraNeeds is null || HomeLife.Catalog(world) is not { } needs || world.ObjectCatalog is not { } catalog) return false;
        if (HomeLife.HomeOf(world, npc) is not { } home || world.FindHousehold(npc.Household!.Value) is not { } household || npc.CurrentLocation != household.Location) return false;
        SettleDue(world, ctx, npc);
        if (PlayerTasks.Of(world, npc) is not null) return false;
        long now = world.CurrentDate.TotalMilliseconds;
        var pieces = HomeFurniture.Of(home, catalog);
        // #261: à toa, a pessoa procura o que a atende já abaixo de SeekBelow (não só quando urgente) e, para variar a rotina, escolhe entre as duas necessidades mais
        // baixas e entre os usos que mais a sobem com o sorteio semeado do mundo (peso = o quanto falta), em vez de sempre o mesmo
        var rng = world.Rng.OneShot($"care-{npc.Id.Value}-{now}");
        var order = needs.Needs.Select(d => (Def: d, Gap: (margin > 0 ? d.UrgentBelow + margin : d.SeekBelow ?? d.UrgentBelow) - (HomeLife.Value(world, npc, d.Id, now) ?? d.Start))).Where(x => x.Gap > 0)
            .OrderByDescending(x => x.Gap * (0.75 + 0.5 * rng.NextDouble())).ThenBy(x => x.Def.Id, StringComparer.Ordinal).ToList();
        foreach (var (def, _) in order)
        {
            // o uso que mais sobe a necessidade de uma vez (taxa × duração, limitado ao que falta para encher), não o de taxa maior mas que dura dois minutos
            double missing = 100 - (HomeLife.Value(world, npc, def.Id, now) ?? def.Start);
            bool pressing = (HomeLife.Value(world, npc, def.Id, now) ?? def.Start) < def.UrgentBelow + margin; // só procurando (acima do urgente): nada que gaste da despensa (ninguém come refeição por tédio)
            var options = new List<(double Rate, int Piece, string Use)>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Spot is null || catalog.Find(pieces[i].Id) is not { Uses: { } uses }) continue;
                foreach (var use in uses)
                    if (use is { Action: null, Hidden: false } && (pressing || use.Consumes is not { Count: > 0 }) && use.Effects.Where(e => e.Need == def.Id && e.PerHour > 0).Sum(e => e.PerHour) is > 0 and var rate)
                        options.Add((Math.Min(rate * use.DurationMin / 60.0, missing), i, use.Id));
            }

            foreach (var option in options.OrderByDescending(o => o.Rate * (0.7 + 0.6 * rng.NextDouble())).ThenBy(o => o.Piece))
                if (Begin(world, ctx, npc, home.Id.Value, option.Piece, option.Use).IsSuccess) return true;
        }

        return false;
    }

    /// <summary>A fila do corpo como o cliente a lê: o uso, o nome dele e o do objeto.</summary>
    public static IReadOnlyList<QueueView>? QueueOf(WorldState world, Npc npc)
    {
        if (npc.UseQueue is not { Count: > 0 } queue) return null;
        var list = new List<QueueView>(queue.Count);
        foreach (var q in queue)
            if (Resolve(world, q.Building, q.Piece, q.Use) is { IsSuccess: true } r)
                list.Add(new QueueView(q.Use, r.Value!.Use.Name, r.Value.Descriptor.Name ?? r.Value.Descriptor.Id));
        return list.Count > 0 ? list : null;
    }

    /// <summary>A visão do uso em curso para o cliente: o que é, em que fase e onde o corpo fica.</summary>
    public static (string Use, string Label, string Phase, string? Pose, double Fx, double Fy, double Facing)? View(WorldState world, BodyTask task)
    {
        if (task.Use is not { } useId || !Resolve(world, task.Building, task.Piece, useId).IsSuccess) return null;
        var t = Resolve(world, task.Building, task.Piece, useId).Value!;
        return (useId, t.Use.Name, task.Doing ? "do" : "walk", t.Use.Pose ?? t.Use.PoseFallback, t.FineX / (double)FinePose.PerCell, t.FineY / (double)FinePose.PerCell, t.Facing);
    }
}
