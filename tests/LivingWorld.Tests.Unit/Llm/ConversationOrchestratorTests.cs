using LivingWorld.AI;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Llm;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Llm;

/// <summary>Fase 11, T6 (LLM-09/10/11), story "Fallback determinístico e resiliência do tick":
/// <see cref="ConversationOrchestrator"/> — caminho feliz, DTO inválido, provider
/// indisponível/erro e orçamento por interação excedido sempre caem em <see
/// cref="FallbackResponder"/> sem gravar memória nova, e o pipeline nunca espera a LLM além do
/// orçamento configurado.</summary>
public class ConversationOrchestratorTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly string[] KnownEmotions = ["neutral", "concerned", "happy", "annoyed", "curious", "afraid"];

    private static ActionCatalog MakeActionCatalog() => ActionCatalog.Create(
        maxDurationHours: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => 8),
        routineSlots: [], defaultAction: ActionType.Idle).Value!;

    private static NeedsRules MakeNeedsRules() => NeedsRules.Create(
        hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
        urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
        continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;

    private static LlmRules MakeLlmRules() => LlmRules.Create(
        hostileTrustThreshold: 20,
        actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible)).Value!;

    private static (WorldState World, TickContext Ctx, Npc Npc, ConversationSessionStore Store, ConversationSession Session) Build()
    {
        var map = ScenarioRunner.DefaultMap(seed: 1);
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            MakeNeedsRules(), MakeActionCatalog(), Stages, familyRules: ScenarioRunner.DefaultFamilyRules);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var store = new ConversationSessionStore();
        var (decision, session) = store.StartConversation(npc, MakeNeedsRules(), MakeLlmRules(), null, ctx, expireAfterTicks: 100);
        if (decision != ConversationStartDecision.Accepted || session is null)
            throw new InvalidOperationException("setup de teste: sessão deveria ter sido aceita");

        return (world, ctx, npc, store, session);
    }

    /// <summary>Provider controlável para os cenários de erro/atraso do gate — nunca chama rede
    /// de verdade (mesmo espírito de <see cref="FakeLlmProvider"/>).</summary>
    private sealed class ScriptedProvider : ILlmProvider
    {
        private readonly Func<LlmContext, CancellationToken, Task<LlmResponse>> _behavior;
        public ScriptedProvider(Func<LlmContext, CancellationToken, Task<LlmResponse>> behavior) => _behavior = behavior;
        public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default) =>
            _behavior(context, cancellationToken);
    }

    [Fact]
    public async Task Happy_path_validates_response_and_applies_effects()
    {
        var (world, ctx, npc, store, session) = Build();
        var effects = new ConversationEffectsApplier();
        var orchestrator = new ConversationOrchestrator(store, effects, new FakeLlmProvider(), KnownEmotions, TimeSpan.FromSeconds(5));

        var turn = await orchestrator.SendMessageAsync(world, npc, session, "oi", ["greet"], [], ctx);

        Assert.NotEmpty(turn.Dialogue);
        Assert.Contains(
            world.CanonicalMemories.Concat(world.VolatileMemories),
            m => m.OwnerId == npc.Id && m.Content == turn.Dialogue);
        var relationship = world.Relationships[new RelationshipKey(npc.Id, ConversationEffectsApplier.PlayerNpcId)];
        Assert.True(relationship.Trust > 0);
    }

    [Fact]
    public async Task Invalid_dto_falls_back_without_recording_new_memory()
    {
        var (world, ctx, npc, store, session) = Build();
        var effects = new ConversationEffectsApplier();
        var provider = new ScriptedProvider((_, _) =>
            Task.FromResult(new LlmResponse("oi", "furious", "none", [], [])));
        var orchestrator = new ConversationOrchestrator(store, effects, provider, KnownEmotions, TimeSpan.FromSeconds(5));

        var turn = await orchestrator.SendMessageAsync(world, npc, session, "oi", ["greet"], [], ctx);

        Assert.Contains(npc.Name, turn.Dialogue);
        Assert.Empty(world.CanonicalMemories.Concat(world.VolatileMemories));
        Assert.DoesNotContain(new RelationshipKey(npc.Id, ConversationEffectsApplier.PlayerNpcId), world.Relationships.Keys);
    }

    [Fact]
    public async Task Provider_error_falls_back_and_never_throws()
    {
        var (world, ctx, npc, store, session) = Build();
        var effects = new ConversationEffectsApplier();
        var provider = new ScriptedProvider((_, _) => throw new InvalidOperationException("provider indisponível"));
        var orchestrator = new ConversationOrchestrator(store, effects, provider, KnownEmotions, TimeSpan.FromSeconds(5));

        var turn = await orchestrator.SendMessageAsync(world, npc, session, "oi", ["greet"], [], ctx);

        Assert.Contains(npc.Name, turn.Dialogue);
        Assert.Empty(world.CanonicalMemories.Concat(world.VolatileMemories));
    }

    [Fact]
    public async Task Budget_exceeded_interrupts_the_external_call_and_falls_back()
    {
        var (world, ctx, npc, store, session) = Build();
        var effects = new ConversationEffectsApplier();
        var provider = new ScriptedProvider(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);
            return new LlmResponse("nunca deveria chegar aqui", "neutral", "none", [], []);
        });
        var orchestrator = new ConversationOrchestrator(store, effects, provider, KnownEmotions, TimeSpan.FromMilliseconds(20));

        var turn = await orchestrator.SendMessageAsync(world, npc, session, "oi", ["greet"], [], ctx);

        Assert.Contains(npc.Name, turn.Dialogue);
        Assert.Empty(world.CanonicalMemories.Concat(world.VolatileMemories));
    }

    [Fact]
    public async Task Message_on_unknown_session_falls_back_without_calling_the_provider()
    {
        var (world, ctx, npc, store, _) = Build();
        var effects = new ConversationEffectsApplier();
        var neverCalled = new ScriptedProvider((_, _) => throw new InvalidOperationException("não deveria ser chamado"));
        var orchestrator = new ConversationOrchestrator(store, effects, neverCalled, KnownEmotions, TimeSpan.FromSeconds(5));
        var unknownSession = new ConversationSession(SessionId: 999, npc.Id, OpenedAtTick: 0, LastTurnTick: 0, IsActive: true);

        var turn = await orchestrator.SendMessageAsync(world, npc, unknownSession, "oi", ["greet"], [], ctx);

        Assert.Contains(npc.Name, turn.Dialogue);
    }
}
