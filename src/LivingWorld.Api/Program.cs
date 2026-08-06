using LivingWorld.AI;
using LivingWorld.Api;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.VisualInput;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Fase 15, T2: mesma conexão sqlite `:memory:` mantida aberta pela vida do processo/factory
// guarda tanto os templates de período (Fase 13, T5) quanto o snapshot canônico do mundo
// (abaixo) — persistência real em disco fica para quando a API ganhar configuração de
// storage, fora do escopo desta task.
var worldDbConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
worldDbConnection.Open();
var worldDbOptions = new DbContextOptionsBuilder<WorldDbContext>().UseSqlite(worldDbConnection).Options;
using (var migrationContext = new WorldDbContext(worldDbOptions))
    migrationContext.Database.Migrate();

// Fase 15, T2 (VTT-01..03): host canônico compartilhado — resolve o SPEC_DEVIATION de Fase 8,
// T15 (mundo efêmero recriado a cada start) carregando o snapshot mais recente de um
// IWorldRepository real; sem snapshot salvo ainda (primeiro start), cria o cenário default e
// já persiste, para o host nunca ficar sem lastro no repositório. `historyRules:
// HistoryRules.Default` liga a separação Crença/Verdade (Fase 12, T7) que GET
// /narratives/reports precisa para calcular confiança.
var worldRepository = new SqliteWorldRepository(new WorldDbContext(worldDbOptions));
var worldRunner = new PersistentWorldRunner(worldRepository, BranchId.Root, snapshotIntervalTicks: 24);
var worldSink = new BufferingWorldEventSink();
var worldClock = new WorldClock(ScenarioRunner.DefaultSystems(), sink: worldSink);

var world = worldRunner.LoadLatest();
if (world is null)
{
    (world, _) = ScenarioRunner.Create(seed: 1, historyRules: HistoryRules.Default);
    worldRunner.Snapshot(world, worldSink);
}

// SimulationHost fica pronto no DI para pausa/velocidade/avanço (task 6) e para o gateway
// realtime (T3) — este host ainda não ticka automaticamente (decisão explícita: T2 troca a
// origem do mundo por persistência real, tick em tempo real fica para uma task futura).
var simulationHost = new SimulationHost(worldClock, world);

// Fase 15, T3 (VTT-02, VTT-10): gateway realtime lê o mesmo relógio do host canônico acima —
// nunca dirige tick nem escreve no mundo (Publish só é chamado pelos projectors futuros com o
// resultado de uma leitura já feita).
var realtimeGateway = new RealtimeGateway(() => world.CurrentDate.TotalHours);

// Fase 11, T7: sessão/efeitos vivem só em memória do processo (mesmo espírito do `world`
// acima) — nunca fazem parte do snapshot/hash canônico do mundo.
var sessions = new ConversationSessionStore();

// Fase 12, T7: crônicas geradas sob demanda pelo endpoint (mesmo padrão de materialização
// sob demanda de NpcInspectionQuery) — idempotente por chave (local, periodStart, periodEnd),
// então chamar de fora do Tick automático do WorldClock é seguro.
var chronicles = new ChronicleGenerationSystem();

// Registrados no DI (em vez de campos `static` em `Program`) para que cada instância de
// `WebApplicationFactory<Program>` (uma por classe de teste) tenha seu próprio `world`/
// `sessions` isolado — campos `static` eram compartilhados entre TODAS as factories do
// processo e colidiam quando classes de teste rodavam em paralelo (xUnit default).
builder.Services.AddSingleton(world);
builder.Services.AddSingleton<IWorldRepository>(worldRepository);
builder.Services.AddSingleton(worldRunner);
builder.Services.AddSingleton(worldClock);
builder.Services.AddSingleton(simulationHost);
builder.Services.AddSingleton(realtimeGateway);
builder.Services.AddSingleton(sessions);

builder.Services.AddDbContext<WorldDbContext>(o => o.UseSqlite(worldDbConnection));
builder.Services.AddScoped<IPeriodTemplateRepository, SqlitePeriodTemplateRepository>();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/npcs/{id:long}", (long id) =>
{
    var result = NpcInspectionQuery.Inspect(world, new NpcId(id));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
});

// LLM_PROVIDER escolhe o provider real de diálogo (ADR-0016); default "fake" para não mudar
// nenhum comportamento existente (gate/testes seguem sempre em FakeLlmProvider, T9).
ILlmProvider llmProvider = builder.Configuration["LLM_PROVIDER"] switch
{
    "ollama" => new OllamaLlmProvider(new HttpClient()),
    "null" => new NullLlmProvider(),
    _ => new FakeLlmProvider(),
};

var effects = new ConversationEffectsApplier();
var orchestrator = new ConversationOrchestrator(
    sessions, effects, llmProvider,
    // União do vocabulário original (FakeLlmProvider) com o enum do schema Ollama (ADR-0016) —
    // superset nunca deixa passar uma emoção inválida, só evita rejeitar toda resposta real do
    // Ollama por causa de um vocabulário pensado só para o fake.
    knownEmotions: ["neutral", "concerned", "happy", "annoyed", "curious", "afraid", "friendly", "angry", "sad", "suspicious"],
    budgetPerInteraction: TimeSpan.FromSeconds(5));
app.MapConversationEndpoints(world, sessions, orchestrator);
app.MapNarrativeEndpoints(world, chronicles);
app.MapPeriodsEndpoints();
app.MapWorldStartEndpoints();
app.MapRealtimeEndpoints();
app.MapVisualInputEndpoints();

app.Run();

public partial class Program;
