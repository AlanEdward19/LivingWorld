using System.Text.Json;
using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Api.Players;

/// <summary><c>PlayerId</c> é opcional com sessão (o jogador vem do token); no modo local é obrigatório.</summary>
public sealed record IncarnateRequest(string? PlayerId);

/// <summary>#252: o corpo para onde o jogador passa (troca sem sumir com o de antes).</summary>
public sealed record PlayerSwitchRequest(long NpcId);
/// <summary><c>Action</c> é o nome da ação (Eat, Sleep, Work, Socialize, Idle, Buy, UsePower); vazio devolve a escolha ao motor.
/// <c>PowerId</c> (AD-196, opcional) é o poder que o NPC porta, exigido com UsePower.</summary>
public sealed record PlayerActionRequest(string? Action, string? PowerId = null);
/// <summary>Passo do jogador (AD-197e): direção −1..1 em cada eixo, intenção e não posição.</summary>
/// AD-197u: <c>Run</c> (opcional) pede correr no <c>walk</c>; o <c>move</c> não usa. #248: <c>Wseq</c> numera o comando de andar (a pose devolve <c>seq</c>, o último aplicado).
public sealed record PlayerMoveRequest(int Dx, int Dy, bool? Run = null, int? Wseq = null);
/// <summary>Golpe do jogador (AD-197e) no NPC ao alcance.</summary>
public sealed record PlayerStrikeRequest(long NpcId);
/// <summary>AD-197d: interagir com o que está perto (<c>GET /players/{playerId}/reach</c> diz o quê): <c>Kind</c> (door, stock, stall,
/// bed, person, house), <c>Id</c>, <c>Verb</c>; <c>ResourceId</c> e <c>Quantity</c> no estoque e na banca.</summary>
public sealed record PlayerInteractRequest(string Kind, long Id, string Verb, int? ResourceId = null, long? Quantity = null);
/// <summary>AD-197c: dormir até a hora do dia <c>UntilHour</c> (0 até horas do dia − 1) ou, sem ela, acelerando até descansar.</summary>
public sealed record PlayerSleepRequest(int? UntilHour = null);
/// <summary>AD-197q: pedir uma amostra do agregado de <c>CityId</c> ao criador de personagem. Filtro opcional (<c>Sex</c>: Female/Male,
/// faixa de idade, ofício por id) e <c>Count</c> (1 a 12; padrão 6). Cada pedido é uma amostra nova, registrada.</summary>
public sealed record PlayerCandidatesRequest(Guid CityId, string? Sex = null, int? MinAge = null, int? MaxAge = null, int? ProfessionId = null,
    int? Count = null);
/// <summary>AD-197q: entrar como <c>NpcId</c>, candidato da amostra <c>Sample</c> de <c>CityId</c> que este jogador pediu.</summary>
public sealed record PlayerCreateRequest(Guid CityId, long NpcId, long Sample);
/// <summary>Sessão nova (ADR-0048): <c>Authorization: Bearer {SessionToken}</c> nas rotas e no canal do jogador.</summary>
public sealed record SessionResponse(string SessionToken, string PlayerId);

/// <summary>Fase 25: <c>POST /sessions</c> (200) emite a sessão do jogador (ADR-0048, AD-197a). <c>POST /players/{npcId}/incarnate</c>
/// (200, 401, 403, 404, 409), <c>POST /players/{playerId}/action</c> (200, 400, 401, 403, 404, 409) e
/// <c>POST /players/{playerId}/logout</c> (200, 401, 403, 404, 409). Com token, o jogador vem da sessão: token inválido 401,
/// <c>playerId</c> da rota ou do corpo que não é o da sessão 403. Sem token: 401 com <c>PLAYER_AUTH=required</c>; no modo local (o
/// padrão) vale o <c>playerId</c> da rota, como antes. Sob pausa do admin, 409. Tudo passa pela fila única
/// (<see cref="PlayerCommandGateway"/>), a mesma do canal <c>/players/ws</c>.</summary>
public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app, PlayerCommandGateway gateway, PlayerSessionTokens tokens, PlayerAuthPolicy policy)
    {
        app.MapPost("/sessions", () =>
        {
            var session = tokens.Create();
            return Results.Ok(new SessionResponse(session.Token, session.PlayerId));
        });

        app.MapPost("/players/{npcId:long}/incarnate", (HttpContext http, long npcId, IncarnateRequest request) =>
            Run(http, tokens, policy.Required, request.PlayerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Incarnate, npcId)));

        // AD-197q: criador de personagem pelo motor — de onde dá para sair (só lê), a amostra do agregado (registrada) e entrar como
        // um dos candidatos (materializa e encarna); agregado vazio ou ninguém no filtro é 409 sem efeito
        app.MapGet("/players/origins", () => Results.Ok(gateway.Origins()));
        app.MapPost("/players/{playerId}/candidates", (HttpContext http, string playerId, PlayerCandidatesRequest request) =>
        {
            if (!TryParseFilter(request.Sex, request.MinAge, request.MaxAge, request.ProfessionId, out var filter))
                return Results.BadRequest(new { error = "sex: Female ou Male" });
            return Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Sample, CityId: request.CityId,
                Filter: filter, Count: request.Count));
        });
        app.MapPost("/players/{playerId}/create", (HttpContext http, string playerId, PlayerCreateRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Create, request.NpcId,
                CityId: request.CityId, SampleNumber: request.Sample)));

        // AD-197t: criador de personagem de verdade — o que a cidade oferece (só lê), a prévia validada pelo motor (só lê, não registra)
        // e a criação (nome, idade, traços, família ou chegada, dinheiro, aparência); incoerência é 409 com o motivo e o mundo intacto
        app.MapGet("/players/origins/{cityId:guid}", (Guid cityId) => Reply(gateway.CityOptions(cityId)));
        app.MapPost("/players/{playerId}/character/preview", (HttpContext http, string playerId, JsonObject body) =>
        {
            if (Identify(http, tokens, policy.Required, playerId) is { } refused) return refused;
            if (gateway.PauseRefusal() is { } paused) return Reply(paused);
            return TryParseSpec(body, out var spec, out var error) ? Reply(gateway.Preview(playerId, spec)) : Results.BadRequest(new { error });
        });
        app.MapPost("/players/{playerId}/character", (HttpContext http, string playerId, JsonObject body) =>
        {
            if (gateway.PauseRefusal() is { } paused) return Reply(paused);
            return !TryParseSpec(body, out var spec, out var error) ? Results.BadRequest(new { error })
                : Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Character, Spec: spec));
        });

        app.MapPost("/players/{playerId}/action", (HttpContext http, string playerId, PlayerActionRequest request) =>
        {
            if (!TryParseAction(request.Action, out var action)) return Results.BadRequest(new { error = "action: desconhecida" });
            return Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Act, Action: action, PowerId: request.PowerId));
        });

        // #252: troca de corpo (sandbox do casal): solta o NPC conduzido, sem sumir, e conduz o outro no mesmo modo de posse
        app.MapPost("/players/{playerId}/switch", (HttpContext http, string playerId, PlayerSwitchRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Switch, request.NpcId)));

        app.MapPost("/players/{playerId}/logout", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Logout)));

        // AD-197e: o passo do jogador substitui POST /visual/player/{id}/move (removida, decisão do dono): só o corpo que a sessão
        // controla, pela fila única, com pausa e registro
        app.MapPost("/players/{playerId}/move", (HttpContext http, string playerId, PlayerMoveRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Move, Dx: request.Dx, Dy: request.Dy)));

        // AD-197k: o passo solto (rumo em milésimos); o canal é o caminho de sempre, a rota serve a quem não o abre
        app.MapPost("/players/{playerId}/walk", (HttpContext http, string playerId, PlayerMoveRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Walk, Dx: request.Dx, Dy: request.Dy, Run: request.Run == true, Seq: request.Wseq ?? 0)));

        // AD-197u: pular (corpo vazio ou {}): o motor decide pelo corpo e pelo fôlego; 409 com o motivo; 200 = a pose e airMs
        app.MapPost("/players/{playerId}/jump", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Jump)));

        // #241: interromper a ação que ocupa o corpo (cortar lenha...): o motor deixa a parte proporcional; 409 sem ação em andamento
        app.MapPost("/players/{playerId}/interrupt", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Interrupt)));

        // #234: esquiva e bloqueio (janela curta, recarga da guarda); 200 = a pose com o combate, 409 com o motivo
        app.MapPost("/players/{playerId}/dodge", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Dodge)));
        app.MapPost("/players/{playerId}/block", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Block)));

        app.MapPost("/players/{playerId}/strike", (HttpContext http, string playerId, PlayerStrikeRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Strike, request.NpcId)));

        // AD-197d: interagir com o que está perto, pela oferta do motor; a recusa da vontade é 409 com o motivo
        app.MapPost("/players/{playerId}/interact", (HttpContext http, string playerId, PlayerInteractRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Interact,
                Interaction: request.Kind, TargetId: request.Id, Verb: request.Verb, ResourceId: request.ResourceId, Quantity: request.Quantity)));
        app.MapGet("/players/{playerId}/reach", (HttpContext http, string playerId) =>
        {
            if (Identify(http, tokens, policy.Required, playerId) is { } refused) return refused;
            return gateway.Reach(playerId) is { } offers ? Results.Ok(new { offers }) : Results.NotFound(new { error = "not_found: jogador não controla nenhum NPC" });
        });

        // #248: a máscara de passos ao redor do corpo (a regra do chão do motor) para o cliente prever o passo com a mesma regra
        app.MapGet("/players/{playerId}/ground", (HttpContext http, string playerId, int? r) =>
        {
            if (Identify(http, tokens, policy.Required, playerId) is { } refused) return refused;
            return gateway.Ground(playerId, r ?? 8) is { } ground ? Results.Ok(ground) : Results.NotFound(new { error = "not_found: jogador não controla nenhum NPC" });
        });

        // AD-197f: convencer a pessoa possuída a ceder (posse comum): tira resistência, uma vez por hora
        app.MapPost("/players/{playerId}/persuade", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Persuade)));

        // AD-197c: pular o tempo, só quem joga sozinho (409 com outro jogador ou sob pausa); o motor acorda por golpe, fogo, fome,
        // sede ou alguém chamando. GET .../time diz o ritmo e se os controles valem agora
        app.MapPost("/players/{playerId}/sleep", (HttpContext http, string playerId, PlayerSleepRequest? request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Sleep, UntilHour: request?.UntilHour)));
        app.MapPost("/players/{playerId}/accelerate", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Accelerate)));
        // #260: a velocidade do tempo do jogador (0 pausa, 1, 2, 3), só quem joga sozinho
        app.MapPost("/players/{playerId}/speed", (HttpContext http, string playerId, PlayerSpeedRequest request) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Speed, Speed: request.Speed)));
        app.MapPost("/players/{playerId}/wake", (HttpContext http, string playerId) =>
            Run(http, tokens, policy.Required, playerId, gateway, new PlayerCommandRequest(PlayerCommandKind.Wake)));
        app.MapGet("/players/{playerId}/time", (HttpContext http, string playerId) =>
            Identify(http, tokens, policy.Required, playerId) is { } refused ? refused : Results.Ok(gateway.Time(playerId)));

        // AD-197e: posse plena é do admin (X-Admin-Token) e do jogador da sessão; a pessoa vira personagem do jogador e fica presa
        app.MapPost("/players/{npcId:long}/possess", (HttpContext http, long npcId, IConfiguration config) =>
        {
            if (LivingWorld.Api.Admin.AdminEndpoints.Authorize(http, config) is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (tokens.PlayerOf(BearerOf(http)) is not { } playerId) return Results.StatusCode(StatusCodes.Status401Unauthorized);
            var reply = gateway.Execute(playerId, new PlayerCommandRequest(PlayerCommandKind.Incarnate, npcId,
                Mode: LivingWorld.Domain.Population.PossessionMode.Full));
            return reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);
        });

        app.MapPlayerHomeEndpoints(gateway, tokens, policy); // #264: modo comprar e construir (leitura)
        app.MapPlayerGoalsEndpoints(gateway, tokens, policy); // #235: metas, habilidades e diário (só leitura)

        // AD-197e: a pessoa presa, vista por quem a possui (posse plena); 404 fora dela
        app.MapGet("/players/{playerId}/captive", (HttpContext http, string playerId) =>
        {
            if (Identify(http, tokens, policy.Required, playerId) is { } refused) return refused;
            return gateway.Captive(playerId) is { } view ? Results.Ok(view) : Results.NotFound(new { error = "not_found: sem posse plena" });
        });
    }

    /// <summary>Nulo quando o pedido pode agir como <paramref name="playerId"/>; senão a resposta de recusa (401/403/400).</summary>
    internal static IResult? Identify(HttpContext http, PlayerSessionTokens tokens, bool authRequired, string playerId)
    {
        if (BearerOf(http) is { } token)
        {
            string? session = tokens.PlayerOf(token);
            if (session is null) return Results.StatusCode(StatusCodes.Status401Unauthorized);
            return session == playerId ? null : Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        return authRequired ? Results.StatusCode(StatusCodes.Status401Unauthorized) : null;
    }

    private static readonly JsonSerializerOptions SpecJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private static IResult Reply(PlayerReply reply) =>
        reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);

    /// <summary>AD-197t: a escolha do criador vinda de JSON (rota ou canal); forma errada ou sexo desconhecido é 400.</summary>
    internal static bool TryParseSpec(JsonNode? node, out CharacterSpec spec, out string error)
    {
        spec = null!;
        error = "";
        try
        {
            var parsed = node?.Deserialize<CharacterSpec>(SpecJson);
            if (parsed is null || parsed.CityId == Guid.Empty) { error = "cityId: obrigatório"; return false; }
            if (parsed.Sex is { } sex && !Enum.IsDefined(sex)) { error = "sex: Female ou Male"; return false; }
            spec = parsed;
            return true;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            error = "character: forma inválida";
            return false;
        }
    }

    /// <summary>AD-197q: o filtro do criador; sexo desconhecido é recusado (400).</summary>
    internal static bool TryParseFilter(string? sex, int? minAge, int? maxAge, int? professionId, out CharacterFilter filter)
    {
        filter = CharacterFilter.None;
        LivingWorld.Domain.Population.Sex? parsed = null;
        if (!string.IsNullOrWhiteSpace(sex))
        {
            if (!Enum.TryParse<LivingWorld.Domain.Population.Sex>(sex, ignoreCase: true, out var value) || !Enum.IsDefined(value)) return false;
            parsed = value;
        }
        filter = new CharacterFilter(parsed, minAge, maxAge, professionId);
        return true;
    }

    internal static bool TryParseAction(string? text, out ActionType? action)
    {
        action = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!Enum.TryParse<ActionType>(text, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)) return false;
        action = parsed;
        return true;
    }

    /// <summary>O token do cabeçalho <c>Authorization: Bearer</c>, ou nulo sem cabeçalho.</summary>
    internal static string? BearerOf(HttpContext http)
    {
        string header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }

    private static IResult Run(HttpContext http, PlayerSessionTokens tokens, bool authRequired, string? claimed, PlayerCommandGateway gateway,
        PlayerCommandRequest command)
    {
        string? playerId;
        if (BearerOf(http) is { } token)
        {
            playerId = tokens.PlayerOf(token);
            if (playerId is null) return Results.StatusCode(StatusCodes.Status401Unauthorized);
            if (!string.IsNullOrWhiteSpace(claimed) && claimed != playerId) return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        else if (authRequired)
        {
            return Results.StatusCode(StatusCodes.Status401Unauthorized);
        }
        else
        {
            playerId = claimed;
            if (string.IsNullOrWhiteSpace(playerId)) return Results.BadRequest(new { error = "playerId: obrigatório" });
        }

        var reply = gateway.Execute(playerId, command);
        return reply.Ok ? Results.Ok(reply.Body) : Results.Json(new { error = reply.Error }, statusCode: reply.Status);
    }
}

/// <summary>#260: corpo do <c>POST /players/{id}/speed</c>.</summary>
public sealed record PlayerSpeedRequest(int Speed);
