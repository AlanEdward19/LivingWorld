using System.Text.Json;
using LivingWorld.AI;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Tests.Llm;

/// <summary>Fase 11, T8 (LLM-12, LLM-14, LLM-15), story "Segurança de rede e injeção" (AC2-3):
/// corpus versionado de tentativas de prompt injection (<c>tests/fixtures/prompt-injection/*.json</c>)
/// — cada entrada é uma "resposta maliciosa" simulada (o que uma LLM comprometida tentaria
/// devolver) rodada através do pipeline real (<see cref="ConversationOrchestrator"/>) via um
/// provider script-ado, nunca por comparação de string/prosa. Três asserts objetivos por
/// entrada (spec.md, "corpus de injeção... valida cada fixture com: ações permitidas, hash
/// canônico inalterado e zero campos fora do schema"). Um <c>[Theory]</c> orientado por arquivo
/// (<see cref="FixtureFiles"/>) cobre toda entrada existente no diretório por construção — uma
/// fixture nova sem o formato esperado quebra a desserialização e reprova o teste.</summary>
public class PromptInjectionSecurityTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);
    private static readonly LifeStageRules Stages = LifeStageRules.Create(childMaxAge: 14, adultMaxAge: 64).Value!;
    private static readonly Personality Neutral = Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;
    private static readonly string[] KnownEmotions = ["neutral", "concerned", "happy", "annoyed", "curious", "afraid"];

    private static readonly HashSet<string> SchemaFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "dialogue", "emotion", "intent", "proposedActions", "memoryCandidates",
    };

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private sealed record Fixture(
        string Id, string Technique, string PlayerUtterance, IReadOnlyList<string> AllowedActions, string MaliciousResponseJson);

    /// <summary>Provider controlável — mesmo espírito do <c>ScriptedProvider</c> de
    /// <c>ConversationOrchestratorTests</c> (T6), copiado aqui porque aquela classe é privada e
    /// esta task só toca arquivos de teste novos em <c>Llm/*Security*Tests.cs</c>.</summary>
    private sealed class ScriptedProvider(Func<LlmContext, CancellationToken, Task<LlmResponse>> behavior) : ILlmProvider
    {
        public Task<LlmResponse> GetResponseAsync(LlmContext context, CancellationToken cancellationToken = default) =>
            behavior(context, cancellationToken);
    }

    public static IEnumerable<object[]> FixtureFiles() =>
        Directory.EnumerateFiles(FixturesDir(), "*.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { f });

    [Fact]
    public void At_least_twenty_fixtures_exist_in_the_corpus()
    {
        int count = Directory.EnumerateFiles(FixturesDir(), "*.json").Count();
        Assert.True(count >= 20, $"corpus tem só {count} fixtures, spec pede >= 20");
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public async Task Fixture_is_rejected_hash_stays_stable_and_no_field_outside_the_schema_survives(string path)
    {
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), CaseInsensitive)!;

        // Assert #3 (zero campos fora do schema): não importa o que a resposta maliciosa tenta
        // colar no JSON bruto (ex.: "systemOverride": true) — o DTO tipado (LlmResponse) só tem
        // os 5 campos do contrato original; nenhum campo extra sobrevive ao round-trip.
        var response = JsonSerializer.Deserialize<LlmResponse>(fixture.MaliciousResponseJson, CaseInsensitive)!;
        var roundTrip = JsonSerializer.SerializeToElement(response);
        foreach (var prop in roundTrip.EnumerateObject())
            Assert.Contains(prop.Name, SchemaFields);

        var (world, ctx, npc, store, session) = BuildAcceptedSession();
        var effects = new ConversationEffectsApplier();
        var provider = new ScriptedProvider((_, _) => Task.FromResult(response));
        var orchestrator = new ConversationOrchestrator(store, effects, provider, KnownEmotions, TimeSpan.FromSeconds(5));

        string hashBefore = WorldSnapshot.CanonicalHash(world);
        var turn = await orchestrator.SendMessageAsync(
            world, npc, session, fixture.PlayerUtterance, fixture.AllowedActions, fixture.AllowedActions, ctx);
        string hashAfter = WorldSnapshot.CanonicalHash(world);

        // Assert #2: hash canônico inalterado byte a byte — a resposta maliciosa nunca vira fato
        // do mundo, aceita ou rejeitada.
        Assert.Equal(hashBefore, hashAfter);

        // Assert #1: proposedActions ⊄ AllowedActions(npc,ctx) -> LlmResponseValidator rejeita
        // tudo -> cai no FallbackResponder, que nunca ecoa proposedActions/memoryCandidates do
        // LLM nem grava memória episódica nova.
        Assert.DoesNotContain(response.ProposedActions.Single(), fixture.AllowedActions);
        Assert.Empty(turn.ProposedActions);
        Assert.Contains(npc.Name, turn.Dialogue);
        Assert.Empty(effects.EpisodicMemoryOf(session.SessionId));
    }

    /// <summary>Par de mutação obrigatório (spec.md, story "Segurança de rede e injeção", AC3):
    /// com <see cref="LlmResponseValidator.EnforceValidation"/> desligada (flag de teste do T5),
    /// o critério "ação fora de AllowedActions é rejeitada" tem que FALHAR — a ação maliciosa
    /// atravessa e vira memória/efeito real. Sem isso o teste acima não prova nada: um validador
    /// sempre-aprova também "passaria" nele.</summary>
    [Fact]
    public async Task Mutation_pair_disabling_the_validator_lets_a_malicious_action_survive()
    {
        var path = Directory.EnumerateFiles(FixturesDir(), "*.json").OrderBy(f => f, StringComparer.Ordinal).First();
        var fixture = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), CaseInsensitive)!;
        var response = JsonSerializer.Deserialize<LlmResponse>(fixture.MaliciousResponseJson, CaseInsensitive)!;

        var (world, ctx, npc, store, session) = BuildAcceptedSession();
        var effects = new ConversationEffectsApplier();
        var provider = new ScriptedProvider((_, _) => Task.FromResult(response));
        var orchestrator = new ConversationOrchestrator(store, effects, provider, KnownEmotions, TimeSpan.FromSeconds(5));

        try
        {
            LlmResponseValidator.EnforceValidation = false;

            var turn = await orchestrator.SendMessageAsync(
                world, npc, session, fixture.PlayerUtterance, fixture.AllowedActions, fixture.AllowedActions, ctx);

            Assert.NotEmpty(turn.ProposedActions);
            Assert.Contains(turn.Dialogue, effects.EpisodicMemoryOf(session.SessionId));
        }
        finally
        {
            LlmResponseValidator.EnforceValidation = true;
        }
    }

    private static (WorldState World, TickContext Ctx, Npc Npc, ConversationSessionStore Store, ConversationSession Session) BuildAcceptedSession()
    {
        var actionCatalog = ActionCatalog.Create(
            maxDurationHours: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => 8),
            routineSlots: [], defaultAction: ActionType.Idle).Value!;
        var needsRules = NeedsRules.Create(
            hungerDecayPerHour: 0, thirstDecayPerHour: 0, sleepDecayPerHour: 0, socialDecayPerHour: 0,
            urgencyThreshold: 70, maxActionSelectionSteps: 10, hysteresisEnabled: false,
            continuityBonus: 0, homelessSleepEfficiency: 0.5).Value!;
        var llmRules = LlmRules.Create(
            hostileTrustThreshold: 20,
            actionCompatibility: Enum.GetValues<ActionType>().ToDictionary(a => a, _ => ConversationCompatibility.Compatible)).Value!;

        var map = ScenarioRunner.DefaultMap(seed: 1);
        var world = new WorldState(
            Calendar, seed: 1, map, ScenarioRunner.DefaultPopulationCatalog, ScenarioRunner.DefaultPopulationRules,
            needsRules, actionCatalog, Stages);
        var location = new CellCoord(1, 1);
        var npc = new Npc(
            new NpcId(1), "npc", Sex.Male, WorldDate.Epoch(Calendar).AddYears(-30), new CultureId(1), location,
            motherId: null, fatherId: null, household: null, health: 100,
            personality: Neutral, profession: ProfessionType.None, currentLocation: location);
        world.AddNpc(npc);
        world.AdvanceNpcIdTo(2);
        var ctx = new TickContext(world, world.Rng, world.Scheduler);

        var store = new ConversationSessionStore();
        var (decision, session) = store.StartConversation(npc, needsRules, llmRules, null, ctx, expireAfterTicks: 100);
        if (decision != ConversationStartDecision.Accepted || session is null)
            throw new InvalidOperationException("setup de teste: sessão deveria ter sido aceita");

        return (world, ctx, npc, store, session);
    }

    private static string FixturesDir() => Path.Combine(FindRepoRoot(), "tests", "fixtures", "prompt-injection");

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LivingWorld.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("LivingWorld.sln não encontrado a partir de " + AppContext.BaseDirectory);
    }
}
