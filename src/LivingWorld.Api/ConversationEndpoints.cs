using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api;

public sealed record ConversationStartRequest(long NpcId);

public sealed record ConversationStartResponse(string Decision, long? SessionId);

public sealed record ConversationSendRequest(long SessionId, string Message);

public sealed record ConversationSendResponse(string Dialogue, string Emotion, string Intent, IReadOnlyList<string> ProposedActions);

public sealed record ConversationEndRequest(long SessionId);

public sealed record ConversationEndResponse(string Result);

/// <summary>Fase 11, T7 (LLM-01..03, story "Sessão de conversa segura", todas as ACs): liga os
/// endpoints HTTP ao pipeline já pronto (T2 <see cref="ConversationSessionStore"/>, T6 <see
/// cref="ConversationOrchestrator"/>) — nenhuma lógica de decisão nova aqui, só tradução
/// request/response e os 404/409 que a spec pede.</summary>
public static class ConversationEndpoints
{
    // ponytail: expiração e allowed-intents/actions reais (AllowedActionsContext(npc,ctx)) ficam
    // fora do escopo desta task — todas as tasks T1-T6 marcam isso "fora do escopo desta task".
    // Lista vazia de ações é a opção mais restritiva (nenhuma ação de mundo liberada por padrão)
    // até uma task futura definir a fonte real; intents são só o vocabulário mínimo do fallback.
    private const long ExpireAfterTicks = 100;
    private static readonly string[] AllowedIntents = ["greet", "ask", "farewell"];
    private static readonly string[] AllowedActions = [];

    private static readonly NeedsRules NeedsRules = ScenarioRunner.DefaultNeedsRules;

    // Sleep é a única ação declarada Forbidden: não dá para acordar alguém para conversar
    // (mesmo raciocínio do exemplo usado nos testes de T2/T3). Todo o resto é Compatible.
    private static readonly LlmRules ConversationLlmRules = LlmRules.Create(
        hostileTrustThreshold: 10,
        actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(
            a => a, a => a == ActionType.Sleep ? ConversationCompatibility.Forbidden : ConversationCompatibility.Compatible)).Value!;

    public static void MapConversationEndpoints(this WebApplication app, WorldHost host, ConversationSessionStore sessions, ConversationOrchestrator orchestrator)
    {
        app.MapPost("/conversations/start", (ConversationStartRequest request) =>
        {
            var world = host.Current;
            var npc = FindNpc(world, request.NpcId);
            if (npc is null) return Results.NotFound();

            var ctx = NewRequestScopedTickContext(world);
            var (decision, session) = sessions.StartConversation(npc, NeedsRules, ConversationLlmRules, relationshipToInitiator: null, ctx, ExpireAfterTicks);

            return Results.Ok(new ConversationStartResponse(decision.ToString(), session?.SessionId));
        });

        app.MapPost("/conversations/send", async (ConversationSendRequest request) =>
        {
            var world = host.Current;
            var session = sessions.Find(request.SessionId);
            if (session is null) return Results.NotFound();

            var npc = FindNpc(world, session.NpcId.Value);
            if (npc is null || !npc.IsAlive)
            {
                sessions.EndConversation(session.SessionId);
                return Results.Conflict("npc-dead");
            }

            if (!session.IsActive) return Results.Conflict("session-ended");

            var ctx = NewRequestScopedTickContext(world);
            var turn = await orchestrator.SendMessageAsync(world, npc, session, request.Message, AllowedIntents, AllowedActions, ctx);

            return Results.Ok(new ConversationSendResponse(turn.Dialogue, turn.Emotion, turn.Intent, turn.ProposedActions));
        });

        app.MapPost("/conversations/end", (ConversationEndRequest request) =>
        {
            var result = sessions.EndConversation(request.SessionId);
            return result == EndConversationResult.Ok ? Results.Ok(new ConversationEndResponse("Ok")) : Results.NotFound();
        });
    }

    private static Npc? FindNpc(WorldState world, long id)
    {
        var npcId = new NpcId(id);
        foreach (var npc in world.Npcs)
            if (npc.Id == npcId) return npc;
        return null;
    }

    // ponytail: `WorldState.Rng`/`.Scheduler` são `internal` (só Simulation/LivingWorld.Tests via
    // InternalsVisibleTo) — Api não pode pegar os streams/scheduler reais do mundo sem uma nova
    // API pública em WorldState.cs, fora do escopo desta task (só arquivos de Api). Nenhum código
    // de conversa (T1-T6) consome `ctx.Rng(...)`, e `ctx.ScheduleEvent` só grava num scheduler
    // descartável — sem custo, porque este host ainda não roda `WorldClock` (mesma limitação já
    // registrada no SPEC_DEVIATION de `Program.cs`: mundo efêmero, sem loop de tick). O tick
    // corrente (`ctx.CurrentTick`) continua correto porque vem de `world.CurrentDate`, não do
    // scheduler/rng descartáveis.
    private static TickContext NewRequestScopedTickContext(WorldState world) =>
        new(world, new WorldRngRegistry(world.Seed), new EventScheduler());
}
