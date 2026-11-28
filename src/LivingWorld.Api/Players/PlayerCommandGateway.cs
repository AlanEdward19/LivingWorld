using LivingWorld.Api.World;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Hosting;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary>Um comando de jogador vindo da rota HTTP ou do canal persistente. <c>Mode</c> só com <c>Incarnate</c> (posse plena é
/// do admin); <c>Dx</c>/<c>Dy</c> com <c>Move</c>; <c>NpcId</c> é o alvo no <c>Incarnate</c> e no <c>Strike</c>. AD-197c: <c>UntilHour</c>
/// (hora do dia) com <c>Sleep</c>; sem ela, dormir acelerando. AD-197d: <c>Interaction</c> (door, stock, stall, bed, person, house),
/// <c>TargetId</c>, <c>Verb</c>, <c>ResourceId</c> e <c>Quantity</c> com <c>Interact</c>. AD-197u: <c>Run</c> com <c>Walk</c> pede correr.</summary>
public sealed record PlayerCommandRequest(PlayerCommandKind Kind, long? NpcId = null, ActionType? Action = null, string? PowerId = null,
    LivingWorld.Domain.Population.PossessionMode Mode = LivingWorld.Domain.Population.PossessionMode.Common, int Dx = 0, int Dy = 0,
    int? UntilHour = null, string? Interaction = null, long? TargetId = null, string? Verb = null, int? ResourceId = null,
    long? Quantity = null, Guid? CityId = null, long? SampleNumber = null, CharacterFilter? Filter = null, int? Count = null,
    CharacterSpec? Spec = null, bool Run = false, int Seq = 0, int? Speed = null, bool UntilReturn = false, int? X = null, int? Y = null, long? BuildingId = null, string? Label = null);

/// <summary>AD-197c: o tempo visto pelo jogador. <c>Rhythm</c>: Fast, RealTime, Accelerated ou Manual; <c>CanSkip</c>: pode dormir
/// acelerado ou acelerar (só quem joga sozinho); <c>Skipping</c>/<c>Sleeping</c>/<c>UntilMs</c>: o pulo em curso;
/// <c>LastWakeReason</c>: o que encerrou o último (golpe, fogo, fome, sede, chamado, hora, descansado, outro jogador, comando).</summary>
public sealed record PlayerTimeView(
    string Rhythm, int TickDeltaMs, double TimeScale, int PlayersPresent, bool CanSkip, bool Skipping, bool Sleeping, long? UntilMs,
    string? LastWakeReason, int Speed = 1, int MaxSpeed = 3, IReadOnlyList<int>? Speeds = null, int WalkMaxSpeed = 10);

/// <summary>A pose do corpo do jogador na resposta de um comando. <c>AirMs</c> (ms no ar) só na resposta do pulo (AD-197u). #248: <c>Seq</c> é o
/// último número de comando de andar aplicado e <c>AtMs</c> o instante de mundo da pose.</summary>
public sealed record PlayerPose(
    string PlayerId, long NpcId, int X, int Y, long? BuildingId, int Floor, long ReadyInMs, double Fx, double Fy, double SpeedMps,
    double CellMeters, bool Running,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? AirMs = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CombatView? Combat = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Simulation.Players.WalkDrag? Ground = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Simulation.Players.BusyView? Busy = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Seq = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] long AtMs = 0,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LivingWorld.Simulation.Objects.QueueView>? Queue = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Simulation.Objects.SocialEvent? Social = null);

/// <summary>A resposta de um comando: status HTTP (200, 400, 404, 409) e o corpo (sucesso) ou o erro.</summary>
public sealed record PlayerReply(int Status, object? Body, string? Error)
{
    public bool Ok => Status == StatusCodes.Status200OK;
}

/// <summary>Fila única de aplicação dos comandos de jogador por branch (ADR-0048, AD-197a): todo comando, venha da rota HTTP ou do
/// canal persistente, é aplicado sob a mesma trava do laço do relógio (<see cref="SavedWorldLibrary.Gate"/>), no limite entre
/// ticks, na ordem em que chega à trava; o aceito vai para o registro do hospedeiro e para o disco antes da resposta. Sob pausa do
/// admin todo comando é recusado com 409 sem mudar o mundo (aprovado pelo dono em 2026-11-23).</summary>
public sealed partial class PlayerCommandGateway(
    WorldHost host, SimulationHost simulation, PlayerService players, SavedWorldLibrary library, PlayerLogStore log,
    LivingWorld.Simulation.Llm.ConversationSessionStore? conversations = null)
{
    public const string PausedError = "conflict: mundo pausado pelo admin";

    /// <summary>Só para o par de mutação do teste: falso desliga a recusa sob pausa (o teste da pausa tem de reprovar).</summary>
    public bool EnforcePause { get; set; } = true;

    public PlayerLogStore Log => log;

    /// <summary>#240: a pausa de popup (pedido do jogador que joga sozinho). O laço não roda tick enquanto ela vale; os comandos do popup
    /// continuam aceitos (só a pausa do admin recusa). Trocável nos testes para um relógio falso.</summary>
    public PopupHold Hold { get; set; } = new(TimeProvider.System);

    private Guid _holdWorld;

    /// <summary>Liga/solta o hold do jogador e lembra de qual mundo ele é (trocar de mundo o derruba).</summary>
    public bool SetHold(string playerId, bool on)
    {
        bool held = Hold.Set(playerId, on);
        if (held) _holdWorld = library.CurrentId;
        return held;
    }

    /// <summary>O mundo está parado por um popup: hold vivo, só um jogador conectado e no máximo um corpo conduzido no mundo.
    /// Um hold pedido em outro mundo (carregar/criar no meio) não vale: o mundo novo anda sem esperar o popup do antigo.</summary>
    public bool PopupHolding
    {
        get
        {
            if (!Hold.IsActive) return false;
            if (_holdWorld != library.CurrentId) { Hold.Clear(); return false; }
            lock (library.Gate) return PlayerService.PlayersPresent(host.Current) <= 1;
        }
    }

    /// <summary>Recusa da pausa do admin antes de ler o pedido (decisão do dono: pausa = 409 em toda rota do jogador).</summary>
    public PlayerReply? PauseRefusal() =>
        EnforcePause && simulation.IsPaused ? new PlayerReply(StatusCodes.Status409Conflict, null, PausedError) : null;

    /// <summary>AD-197q: nomes do período (ofício) para os cartões do criador de personagem; sem ele, só os ids.</summary>
    public LivingWorld.Api.Visual.Observable.PresentationNameResolver? Names { get; set; }

    public PlayerReply Execute(string playerId, PlayerCommandRequest command)
    {
        lock (library.Gate)
        {
            if (EnforcePause && simulation.IsPaused) return new PlayerReply(StatusCodes.Status409Conflict, null, PausedError);
            var world = host.Current;
            log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs);
            var before = players.Log.Last;
            var reply = command.Kind switch
            {
                PlayerCommandKind.Incarnate => Map(players.Incarnate(world, new NpcId(command.NpcId ?? -1), playerId, command.Mode),
                    _ => new { npcId = command.NpcId, playerId }),
                PlayerCommandKind.Switch => Map(players.Switch(world, new NpcId(command.NpcId ?? -1), playerId),
                    _ => new { npcId = command.NpcId, playerId }), // #252: troca de corpo sem sumir
                PlayerCommandKind.Move => Map(players.Move(world, playerId, command.Dx, command.Dy),
                    _ => PoseOf(world, playerId)),
                PlayerCommandKind.Strike => Map(players.Strike(world, playerId, new NpcId(command.NpcId ?? -1)),
                    hit => StrikeView(world, playerId, hit)),
                PlayerCommandKind.Dodge => Map(players.Guard(world, playerId, CombatGuard.Dodge), _ => PoseOf(world, playerId)),
                PlayerCommandKind.Block => Map(players.Guard(world, playerId, CombatGuard.Block), _ => PoseOf(world, playerId)),
                PlayerCommandKind.Persuade => Map(players.Persuade(world, playerId), _ => new { playerId }),
                PlayerCommandKind.Act => Map(players.Act(world, playerId, command.Action, command.PowerId),
                    _ => new { playerId, Action = command.Action?.ToString() }),
                PlayerCommandKind.Sleep when command.UntilHour is { } bad && (bad < 0 || bad >= world.Calendar.HoursPerDay) =>
                    new PlayerReply(StatusCodes.Status400BadRequest, null, "untilHour: fora das horas do dia"),
                PlayerCommandKind.Sleep => Map(players.Sleep(world, playerId,
                        command.UntilHour is { } hour ? PlayerService.NextHourOfDayMs(world, hour) : null),
                    _ => TimeOf(world, playerId)),
                PlayerCommandKind.Speed => Map(players.SetSpeed(world, simulation, playerId, command.Speed ?? -1), _ => TimeOf(world, playerId)),
                PlayerCommandKind.Accelerate when command.UntilReturn && (PlayerCommands.ControlledBy(world, playerId) is not { } body
                    || LivingWorld.Simulation.Careers.CareerWork.NextReturnMs(world, body) is null) =>
                    new PlayerReply(StatusCodes.Status409Conflict, null, $"{PlayerCommands.Conflict} ninguém da casa está no trabalho"), // #267
                PlayerCommandKind.Accelerate => Map(players.Accelerate(world, playerId, command.UntilReturn
                        ? LivingWorld.Simulation.Careers.CareerWork.NextReturnMs(world, PlayerCommands.ControlledBy(world, playerId)!) : null),
                    _ => TimeOf(world, playerId)),
                PlayerCommandKind.Wake => players.Skip?.PlayerId == playerId
                    ? Map(players.EndSkip(world, "jogador"), _ => TimeOf(world, playerId))
                    : new PlayerReply(StatusCodes.Status409Conflict, null, $"{PlayerCommands.Conflict} sem pulo de tempo em curso"),
                PlayerCommandKind.Walk => Map(players.Walk(world, playerId, command.Dx, command.Dy, command.Run, command.Seq), _ => PoseOf(world, playerId)),
                PlayerCommandKind.Jump => Map(players.Jump(world, playerId), air => PoseOf(world, playerId, air)),
                PlayerCommandKind.Interrupt => Map(players.Interrupt(world, playerId), _ => PoseOf(world, playerId)),
                PlayerCommandKind.Go => Map(players.Go(world, playerId, command.X, command.Y, command.NpcId, command.BuildingId, command.Label), _ => PoseOf(world, playerId)), // #267
                PlayerCommandKind.Interact => Map(players.Interact(world, playerId, command.Interaction ?? "", command.TargetId ?? -1,
                    command.Verb ?? "", command.ResourceId, command.Quantity), done => Interacted(world, playerId, done)),
                PlayerCommandKind.Sample => Map(players.Sample(world, playerId, new CityId(command.CityId ?? Guid.Empty),
                    command.Filter ?? CharacterFilter.None, command.Count ?? CharacterCreation.DefaultCount), sample => SampleView(world, sample)),
                PlayerCommandKind.Create => Map(players.Create(world, playerId, new CityId(command.CityId ?? Guid.Empty),
                    new NpcId(command.NpcId ?? -1), command.SampleNumber ?? -1),
                    npc => new { npcId = npc.Id.Value, playerId, cityId = npc.City.Value }),
                PlayerCommandKind.Character when command.Spec is null => new PlayerReply(StatusCodes.Status400BadRequest, null, "spec: obrigatório"),
                PlayerCommandKind.Character => Map(players.Character(world, playerId, command.Spec!),
                    npc => new
                    {
                        npcId = npc.Id.Value,
                        playerId,
                        cityId = npc.City.Value,
                        character = CharacterView(world, npc, command.Spec!.Role ?? new CharacterRole(), npc.Wallet.Amount,
                            command.Spec.Role?.RelativeId is { } relative ? world.FindNpc(new NpcId(relative)) : null),
                    }),
                PlayerCommandKind.Logout => Map(players.Logout(world, playerId),
                    npc => new { npcId = npc.Id.Value, playerId, disappearedAtTick = npc.DisappearedAtTick }),
                _ => new PlayerReply(StatusCodes.Status400BadRequest, null, "comando: não é de jogador"),
            };
            if (players.Log.Last is { } accepted && !ReferenceEquals(accepted, before)) AppendSince(before);
            return reply;
        }
    }

    /// <summary>Grava no disco o que o registro ganhou depois de <paramref name="before"/> (um comando pode registrar dois: o fim do
    /// pulo de tempo e o próprio comando).</summary>
    private void AppendSince(PlayerCommandRecord? before)
    {
        int from = before is null ? 0 : (int)before.Sequence + 1;
        foreach (var record in players.Log.Since(from)) log.Append(record);
    }

    /// <summary>AD-197q: as cidades de onde dá para sair como pessoa nova: quantos o agregado de cada uma tem e quantos já são
    /// detalhados. Só lê.</summary>
    public object Origins()
    {
        lock (library.Gate)
        {
            var world = host.Current;
            var cities = world.ActiveCities()
                .Select(c => new
                {
                    cityId = c.Id.Value,
                    name = c.Name,
                    aggregateCount = c.AggregatePool.Count,
                    detailedCount = world.Npcs.Count(n => n.IsAlive && n.City == c.Id),
                })
                .OrderByDescending(c => c.aggregateCount).ThenBy(c => c.name, StringComparer.Ordinal)
                .ToList();
            return new { cities };
        }
    }

    /// <summary>AD-197q: a amostra como o cliente a mostra (nomes do ofício e da cidade de nascimento resolvidos aqui).</summary>
    private object SampleView(LivingWorld.Simulation.Core.WorldState world, CharacterSample sample)
    {
        var names = Names?.For(world.PeriodId);
        string CityName(Guid id) => world.FindCity(new CityId(id))?.Name ?? "";
        return new
        {
            cityId = sample.CityId,
            cityName = CityName(sample.CityId),
            sample = sample.Sample,
            aggregateCount = sample.AggregateCount,
            matching = sample.Matching,
            candidates = sample.Candidates.Select(c => new
            {
                npcId = c.NpcId,
                name = c.Name,
                sex = c.Sex.ToString(),
                ageYears = c.AgeYears,
                professionId = c.ProfessionId,
                profession = names?.Profession(c.ProfessionId) ?? "",
                cultureId = c.CultureId,
                wealth = c.Wealth,
                health = c.Health,
                personality = c.Personality,
                bornInCityId = c.BornInCityId,
                bornIn = CityName(c.BornInCityId),
                yearsInCity = c.YearsInCity,
                willPrevents = c.WillPrevents,
            }).ToList(),
        };
    }

    /// <summary>AD-197c: o tempo como o jogador o vê: o ritmo em vigor, se pode pular o tempo, o pulo em curso e o que o acordou.</summary>
    public PlayerTimeView Time(string playerId)
    {
        lock (library.Gate) return TimeOf(host.Current, playerId);
    }

    private PlayerTimeView TimeOf(LivingWorld.Simulation.Core.WorldState world, string playerId)
    {
        var plan = simulation.LastPlan;
        var skip = players.Skip?.PlayerId == playerId ? players.Skip : null;
        return new PlayerTimeView(
            plan.Mode.ToString(), plan.DeltaMs, world.Players.Time.TimeScale, PlayerService.PlayersPresent(world),
            simulation.AutoRhythm && PlayerService.CanSkip(world, playerId) && !simulation.IsPaused,
            skip is not null, skip?.Sleeping == true, skip?.UntilMs, players.LastWakeReason,
            simulation.EffectiveSpeed(world), world.Players.Time.MaxPlayerSpeed, world.Players.Time.SpeedList, world.Players.Time.WalkMaxSpeed);
    }

    /// <summary>Avanço do admin (<c>step</c>, <c>advance-year</c>) pelo registro do jogador (AD-197c): o replay roda os mesmos ticks.</summary>
    public PlayerReply Advance(string actor, long ticks, int deltaMs)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs);
            var before = players.Log.Last;
            var reply = Map(players.Advance(world, host.Clock, actor, ticks, deltaMs), _ => new { ticks, deltaMs });
            if (players.Log.Last is { } accepted && !ReferenceEquals(accepted, before)) AppendSince(before);
            return reply;
        }
    }

    /// <summary>A posição autoritativa do corpo depois do passo (o cliente reconcilia por ela). AD-197d: <c>buildingId</c> do prédio
    /// em cuja planta o corpo está (nulo na rua). AD-197u: <c>running</c> (corre agora) e, só na resposta do pulo, <c>airMs</c>.</summary>
    /// <summary>#259: até quanto tempo de mundo depois de uma interação social ela segue na pose como "o último fato" (o cliente avisa uma vez por instante).</summary>
    private const long SocialNoticeMs = 5 * 60_000;

    private PlayerPose PoseOf(LivingWorld.Simulation.Core.WorldState world, string playerId, int? airMs = null)
    {
        if (PlayerCommands.ControlledBy(world, playerId) is not { } npc)
            return new PlayerPose(playerId, -1L, 0, 0, null, 0, 0L, 0.0, 0.0, 0.0, world.Players.CellM, false, airMs);
        bool running = npc.Fine is { Run: true } && (npc.Fine.HeadingX != 0 || npc.Fine.HeadingY != 0);
        return new PlayerPose(
            playerId,
            npc.Id.Value,
            npc.CurrentLocation.X,
            npc.CurrentLocation.Y,
            PlayerGround.InsideBuilding(world, npc),
            PlayerGround.FloorOf(npc),
            Math.Max(0, npc.StepReadyAtMs - world.CurrentDate.TotalMilliseconds),
            (npc.Fine?.X ?? npc.CurrentLocation.X * FinePose.PerCell + 500) / (double)FinePose.PerCell,
            (npc.Fine?.Y ?? npc.CurrentLocation.Y * FinePose.PerCell + 500) / (double)FinePose.PerCell,
            WalkSpeed(world, npc, running),
            world.Players.CellM,
            running,
            airMs,
            PlayerCombat.View(world, npc),
            WalkGround(world, npc),
            LivingWorld.Simulation.Players.PlayerTasks.Of(world, npc) is { } busy
                ? busy with { Accelerated = players.Skip is { Task: true } skip && skip.PlayerId == playerId }
                : null,
            npc.Fine?.Seq ?? 0, // #248: o último número de comando de andar que o motor aplicou; o cliente reaplica os seguintes
            world.CurrentDate.TotalMilliseconds, // #248: o instante de mundo (ms) desta pose
            LivingWorld.Simulation.Objects.ObjectUsage.QueueOf(world, npc), // #255: a fila de ações do corpo
            LivingWorld.Simulation.Objects.SocialInteractions.LastFor(world, npc, SocialNoticeMs)); // #259: o último fato social do corpo, para o aviso curto
    }

    /// <summary>#234: a resposta do golpe: o que houve, o dano, a reação do alvo e a recarga (o que o cliente anima), mais a pose.</summary>
    private object StrikeView(LivingWorld.Simulation.Core.WorldState world, string playerId, StrikeResult hit) => new
    {
        playerId,
        targetId = hit.TargetId,
        outcome = hit.Outcome.ToString(),
        damage = hit.Damage,
        lethal = hit.Lethal,
        reaction = hit.Reaction == CombatReaction.None ? null : hit.Reaction.ToString(),
        reactionMs = hit.Reaction == CombatReaction.None ? 0L : Math.Max(0L, hit.ReactionUntilMs - world.CurrentDate.TotalMilliseconds),
        readyInMs = hit.ReadyInMs,
        pose = PoseOf(world, playerId),
    };

    /// <summary>AD-197k: a velocidade do corpo no rumo em que anda (parado: para o lado de +x), em m/s, para o cliente prever o passo;
    /// correndo (AD-197u), a da corrida.</summary>
    private static double WalkSpeed(LivingWorld.Simulation.Core.WorldState world, LivingWorld.Domain.Population.Npc npc, bool running)
    {
        var at = npc.CurrentLocation;
        int hx = Math.Sign(npc.Fine?.HeadingX ?? 0), hy = Math.Sign(npc.Fine?.HeadingY ?? 0);
        var ahead = hx == 0 && hy == 0 ? new LivingWorld.Domain.Geography.CellCoord(at.X + 1, at.Y)
            : new LivingWorld.Domain.Geography.CellCoord(at.X + hx, at.Y + hy);
        return world.Map.TryGetCell(ahead, out _) ? PlayerWalk.SpeedMps(world, npc, at, ahead, running) : world.Players.WalkMps;
    }

    /// <summary>#238: o chão sob o corpo e o que atrasa o passo (terreno, carga, sono, tempo, corpo), no mesmo rumo de <see cref="WalkSpeed"/>.</summary>
    private static LivingWorld.Simulation.Players.WalkDrag? WalkGround(LivingWorld.Simulation.Core.WorldState world, LivingWorld.Domain.Population.Npc npc)
    {
        var at = npc.CurrentLocation;
        int hx = Math.Sign(npc.Fine?.HeadingX ?? 0), hy = Math.Sign(npc.Fine?.HeadingY ?? 0);
        var ahead = hx == 0 && hy == 0 ? new LivingWorld.Domain.Geography.CellCoord(at.X + 1, at.Y)
            : new LivingWorld.Domain.Geography.CellCoord(at.X + hx, at.Y + hy);
        return npc.Interior is null && world.Map.TryGetCell(ahead, out _) ? LivingWorld.Simulation.Players.PlayerWalk.DragOf(world, npc, at, ahead) : null;
    }

    /// <summary>AD-197d: o que a interação fez, a pose depois dela e, na conversa aceita, a sessão aberta para o chat de sempre.</summary>
    private object Interacted(LivingWorld.Simulation.Core.WorldState world, string playerId, InteractionResult done)
    {
        long? session = null;
        if (done is { Kind: "person", Verb: "talk" } && conversations is not null && world.FindNpc(new NpcId(done.Id)) is { } other)
        {
            var ctx = new LivingWorld.Simulation.Scheduling.TickContext(world, new WorldRngRegistry(world.Seed),
                new LivingWorld.Simulation.Scheduling.EventScheduler());
            session = conversations.StartConversation(other, world.NeedsRules, LivingWorld.Simulation.Llm.ConversationAvailabilityPolicy.PlayerRules,
                null, ctx, LivingWorld.Api.Conversation.ConversationEndpoints.ExpireAfterTicks).Session?.SessionId;
        }
        return new { done.Kind, done.Id, done.Verb, done.Quantity, done.Value, sessionId = session, pose = PoseOf(world, playerId), done.Outcome, done.Note };
    }

    /// <summary>#243: as árvores tocadas (cortadas em parte ou até o toco) com a madeira que cada uma tem agora: o que o cliente desenha em
    /// lugar da árvore natural. Esparso: só as tocadas; a árvore que ninguém cortou segue a regra do cliente.</summary>
    public object Trees()
    {
        lock (library.Gate)
        {
            var world = host.Current;
            return new
            {
                trees = LivingWorld.Simulation.Players.PlayerNature.Touched(world)
                    .Select(t => new { x = t.Cell.X, y = t.Cell.Y, left = t.Left, capacity = t.Capacity, stage = LivingWorld.Simulation.Players.PlayerNature.StageOf(t.Left, t.Capacity) })
                    .ToList(),
                // #258: onde o cenário não tem árvore (quintal limpo, rua e calçada): o cliente não as desenha ali
                clear = LivingWorld.Simulation.Players.PlayerNature.Clear(world).Select(z => new { x = z.X, y = z.Y, w = z.Width, h = z.Height }).ToList(),
            };
        }
    }

    /// <summary>#248: a máscara de passos ao redor do corpo (a regra do chão do motor: parede, porta, soleira, canto, entre prédios, borda) e
    /// quantas pessoas a célula comporta: o cliente prevê o passo consultando isto, sem regra própria. <c>x</c>/<c>y</c> é a célula do canto
    /// de cima à esquerda; <c>mask</c>, <c>size</c>×<c>size</c> linha a linha, um bit por vizinha (<c>(dy+1)*3+(dx+1)</c>). Nulo sem corpo.</summary>
    public object? Ground(string playerId, int radius)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            if (PlayerCommands.ControlledBy(world, playerId) is not { } npc) return null;
            radius = Math.Clamp(radius, 2, 12);
            var at = PlayerGround.PoseOf(world, npc);
            int floor = PlayerGround.FloorOf(npc);
            return new
            {
                x = at.X - radius,
                y = at.Y - radius,
                size = 2 * radius + 1,
                floor,
                buildingId = PlayerGround.InsideBuilding(world, npc),
                crowdLimit = world.Players.CellCrowd,
                mask = PlayerGround.Mask(world, floor, npc.Interior?.Building, at, radius),
            };
        }
    }

    /// <summary>AD-197d: o que o corpo do jogador alcança agora (a oferta do motor); nulo sem corpo.</summary>
    public IReadOnlyList<InteractionOffer>? Reach(string playerId)
    {
        lock (library.Gate) return PlayerService.Reach(host.Current, playerId);
    }

    /// <summary>O que a pessoa presa vê e sente (posse plena, AD-197e), só para quem a possui; nulo se não há posse plena.</summary>
    public CaptiveView? Captive(string playerId)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            return PlayerCommands.ControlledBy(world, playerId) is { } npc ? PossessionWill.View(world, npc, world.CurrentDate.TotalHours) : null;
        }
    }

    /// <summary>Troca a duração do tick do mundo (ADR-0050) pela mesma fila e pelo mesmo registro dos comandos: o replay reproduz a
    /// sequência de Δ. Quem pede é o admin (<paramref name="actor"/>).</summary>
    public PlayerReply SetTickDelta(string actor, int deltaMs)
    {
        lock (library.Gate)
        {
            var world = host.Current;
            log.BeforeCommand(library.CurrentId.ToString("D"), world, players.Log.TickDeltaMs);
            var before = players.Log.Last;
            var reply = Map(players.SetTickDelta(world, actor, deltaMs), _ => new { tickDeltaMs = deltaMs });
            if (players.Log.Last is { } accepted && !ReferenceEquals(accepted, before)) AppendSince(before);
            return reply;
        }
    }

    private static PlayerReply Map<T>(Result<T> result, Func<T, object> ok)
    {
        if (result.IsSuccess) return new PlayerReply(StatusCodes.Status200OK, ok(result.Value!), null);
        string error = result.Error!;
        if (error.StartsWith(PlayerCommands.NotFound, StringComparison.Ordinal)) return new PlayerReply(StatusCodes.Status404NotFound, null, error);
        if (error.StartsWith(PlayerCommands.Conflict, StringComparison.Ordinal)) return new PlayerReply(StatusCodes.Status409Conflict, null, error);
        return new PlayerReply(StatusCodes.Status400BadRequest, null, error);
    }
}
