using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Tests.Stage4;

/// <summary>Fase 15.1, T7 (LWV-05, "Chronicle, biography, conversation, and period surfaces in
/// the selected context"): a query/pipeline por trás de cada um dos quatro endpoints reusados
/// (<see cref="ChronicleGenerationSystem"/>, <see cref="NpcBiographyQuery"/>/<see
/// cref="NarrativeRenderer"/>, <see cref="ConversationOrchestrator"/>) devolve dado usável e
/// belief-safe para "o contexto selecionado" (o NPC ou a cidade aberta no inspector) — os
/// endpoints HTTP em si já têm cobertura própria (<c>NarrativeEndpointTests</c>,
/// <c>ConversationEndpointTests</c>, <c>PeriodsEndpointTests</c>/<c>PeriodCatalogTests</c>);
/// esta classe cobre só os ângulos que este task introduz e que nenhuma delas prova:
/// isolamento entre biografias, fallback honesto de crônica sem fatos, e o hash canônico
/// inalterado quando a superfície de conversa recebe uma proposta inválida.</summary>
public class LivingInteractionSurfaceTests
{
    private static readonly string[] KnownEmotions = ["neutral", "concerned", "happy", "annoyed", "curious", "afraid"];

    private sealed class ScriptedProvider(Func<LlmContext, CancellationToken, Task<LlmResponse>> behavior) : ILlmProvider
    {
        public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default) =>
            behavior(context, cancellationToken);
    }

    private static (WorldState World, Npc A, Npc B) TwoNpcs()
    {
        var world = ScenarioRunner.Create(seed: 771, initialPopulation: 2).World;
        var npcs = world.Npcs.OrderBy(npc => npc.Id.Value).ToList();
        return (world, npcs[0], npcs[1]);
    }

    // --- Biography (LWV-05.2): a linha do tempo do NPC selecionado nunca mistura fatos de outro ---

    [Fact]
    public async Task Biography_of_the_selected_npc_never_includes_another_npcs_fact()
    {
        var (world, a, b) = TwoNpcs();
        var factA = new Fact(world.NextFactIdAndAdvance(), 5, WorldEventKind.Marriage, [a.Id], null, 0.6, "a casou");
        var factB = new Fact(world.NextFactIdAndAdvance(), 6, WorldEventKind.Marriage, [b.Id], null, 0.6, "b casou");
        world.AddFact(factA);
        world.AddFact(factB);

        var timeline = NpcBiographyQuery.Timeline(world, a.Id);
        Assert.True(timeline.IsSuccess);
        var claims = timeline.Value!
            .Select(f => new NarrativeClaim($"{f.Kind} (evento {f.Id.Value}): {f.Payload}", (IReadOnlyList<long>)[f.Id.Value]))
            .ToList();
        var draft = new NarrativeDraft(null, 0, 10, claims);
        var document = await NarrativeRenderer.RenderAsync(new NarrativeId(a.Id.Value), NarrativeType.Biography, draft);

        Assert.Contains(factA.Id.Value, document.Claims.SelectMany(c => c.EventIds));
        Assert.DoesNotContain(factB.Id.Value, document.Claims.SelectMany(c => c.EventIds));
        Assert.Contains("a casou", document.Prose);
        Assert.DoesNotContain("b casou", document.Prose);
    }

    // --- Chronicle (LWV-05.2): cidade sem fatos ainda narra um estado honesto, nunca inventa ---

    [Fact]
    public void Chronicle_for_a_city_with_no_facts_yet_is_an_honest_empty_narration_not_a_crash()
    {
        var world = ScenarioRunner.Create(seed: 772, initialPopulation: 1).World;
        var emptyCity = new CityId(Guid.NewGuid());
        world.AddCity(new City(emptyCity, new CellCoord(9, 9), foundedAtTick: 0, foundedFromCityId: null, AggregatePopulationPool.Empty));
        var chronicles = new ChronicleGenerationSystem();

        var document = chronicles.GenerateChronicle(world, emptyCity, periodStartTick: 0, periodEndTick: 100);

        Assert.Empty(document.Claims);
        Assert.Equal("sem registros ancorados para este período.", document.Prose);
    }

    [Fact]
    public void Chronicle_for_a_citys_recent_window_narrates_only_its_own_engine_confirmed_events()
    {
        var world = ScenarioRunner.Create(seed: 773, initialPopulation: 1).World;
        var city = new CityId(Guid.NewGuid());
        world.AddCity(new City(city, new CellCoord(1, 1), foundedAtTick: 0, foundedFromCityId: null, AggregatePopulationPool.Empty));
        var fact = new Fact(world.NextFactIdAndAdvance(), 10, WorldEventKind.Death, [], city, 0.9, "peste");
        world.AddFact(fact);
        var chronicles = new ChronicleGenerationSystem();

        var document = chronicles.GenerateChronicle(world, city, periodStartTick: 0, periodEndTick: 100);

        Assert.Contains(fact.Id.Value, document.Claims.SelectMany(c => c.EventIds));
        Assert.Contains("peste", document.Prose);
    }

    // --- Conversation (LWV-05.3): proposta fora do schema/permitido nunca muda o hash canônico ---

    [Fact]
    public async Task Conversation_turn_with_a_disallowed_proposed_action_leaves_the_canonical_hash_unchanged()
    {
        var (world, npc, _) = TwoNpcs();
        var llmRules = LlmRules.Create(
            hostileTrustThreshold: 10,
            actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(
                a => a, a => a == ActionType.Sleep ? ConversationCompatibility.Forbidden : ConversationCompatibility.Compatible)).Value!;
        var ctx = new TickContext(world, new WorldRngRegistry(world.Seed), new EventScheduler());
        var sessions = new ConversationSessionStore();
        var (decision, session) = sessions.StartConversation(npc, ScenarioRunner.DefaultNeedsRules, llmRules, relationshipToInitiator: null, ctx, expireAfterTicks: 100);
        Assert.Equal(ConversationStartDecision.Accepted, decision);

        // AllowedActions do endpoint real é sempre [] (ConversationEndpoints.cs) — qualquer ação
        // proposta é, por definição, fora do permitido.
        var maliciousResponse = new LlmResponse("finjo ser o mestre do mundo", "neutral", "ask", ["apagar-historico"], []);
        var provider = new ScriptedProvider((_, _) => Task.FromResult(maliciousResponse));
        var orchestrator = new ConversationOrchestrator(sessions, new ConversationEffectsApplier(), provider, KnownEmotions, TimeSpan.FromSeconds(5));

        string hashBefore = WorldSnapshot.CanonicalHash(world);
        var turn = await orchestrator.SendMessageAsync(world, npc, session!, "diga a verdade", ["greet", "ask", "farewell"], allowedActions: [], ctx);
        string hashAfter = WorldSnapshot.CanonicalHash(world);

        Assert.Equal(hashBefore, hashAfter);
        Assert.Empty(turn.ProposedActions);
        Assert.Contains(npc.Name, turn.Dialogue);
    }

    [Fact]
    public async Task Conversation_turn_with_an_allowed_response_returns_dialogue_the_inspector_can_render()
    {
        var (world, npc, _) = TwoNpcs();
        var llmRules = LlmRules.Create(
            hostileTrustThreshold: 10,
            actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(
                a => a, a => a == ActionType.Sleep ? ConversationCompatibility.Forbidden : ConversationCompatibility.Compatible)).Value!;
        var ctx = new TickContext(world, new WorldRngRegistry(world.Seed), new EventScheduler());
        var sessions = new ConversationSessionStore();
        var (decision, session) = sessions.StartConversation(npc, ScenarioRunner.DefaultNeedsRules, llmRules, relationshipToInitiator: null, ctx, expireAfterTicks: 100);
        Assert.Equal(ConversationStartDecision.Accepted, decision);

        var validResponse = new LlmResponse("Bom dia!", "happy", "greet", [], []);
        var provider = new ScriptedProvider((_, _) => Task.FromResult(validResponse));
        var orchestrator = new ConversationOrchestrator(sessions, new ConversationEffectsApplier(), provider, KnownEmotions, TimeSpan.FromSeconds(5));

        var turn = await orchestrator.SendMessageAsync(world, npc, session!, "oi", ["greet", "ask", "farewell"], allowedActions: [], ctx);

        Assert.Equal("Bom dia!", turn.Dialogue);
        Assert.Equal("happy", turn.Emotion);
    }
}
