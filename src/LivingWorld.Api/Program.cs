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

// Fase 15, T9: documento OpenAPI (/openapi/v1.json) — fonte pros tipos TS do cliente web,
// gerados por scripts/generate-web-types.sh a partir deste endpoint.
builder.Services.AddOpenApi();

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

// Feature ad-hoc "criar mundo": wrapper mutável — antes dele `world` era capturado por
// closure em vários lugares (gateway realtime, endpoints de conversa/narrativa, GET /npcs/{id})
// e nada no processo conseguia trocar de instância em runtime. Troca real acontece em
// `WorldCreateEndpoints` via `host.Replace`.
var worldHost = new WorldHost(world, worldClock);

// SimulationHost fica pronto no DI para pausa/velocidade/avanço (task 6) e para o gateway
// realtime (T3) — este host ainda não ticka automaticamente (decisão explícita: T2 troca a
// origem do mundo por persistência real, tick em tempo real fica para uma task futura).
var simulationHost = new SimulationHost(worldHost);

// Fase 15, T3 (VTT-02, VTT-10): gateway realtime lê o mesmo relógio do host canônico acima —
// nunca dirige tick nem escreve no mundo (Publish só é chamado pelos projectors futuros com o
// resultado de uma leitura já feita).
var realtimeGateway = new RealtimeGateway(() => worldHost.Current.CurrentDate.TotalHours);

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
builder.Services.AddSingleton(worldHost);
// Transient (não singleton fixo): lê `host.Current` a cada resolução, então reflete qualquer
// troca feita por `POST /worlds/create` sem precisar reiniciar o processo. Transient (não
// Scoped) porque alguns testes resolvem direto de `factory.Services` (root provider), que não
// consegue instanciar um serviço Scoped fora de um scope.
builder.Services.AddTransient(sp => sp.GetRequiredService<WorldHost>().Current);
builder.Services.AddSingleton<IWorldRepository>(worldRepository);
builder.Services.AddSingleton(worldRunner);
builder.Services.AddSingleton(worldClock);
builder.Services.AddSingleton(worldSink);
builder.Services.AddSingleton(simulationHost);
builder.Services.AddSingleton(realtimeGateway);
builder.Services.AddSingleton(sessions);

builder.Services.AddDbContext<WorldDbContext>(o => o.UseSqlite(worldDbConnection));
builder.Services.AddScoped<IPeriodTemplateRepository, SqlitePeriodTemplateRepository>();

var app = builder.Build();

// UX pass 3: repositório de períodos começa vazio em todo processo novo — sem isso, o wizard de
// "criar mundo" não teria nenhum template real pra oferecer (ver DefaultPeriodSeeder.cs).
using (var seedScope = app.Services.CreateScope())
    DefaultPeriodSeeder.SeedIfEmpty(seedScope.ServiceProvider.GetRequiredService<IPeriodTemplateRepository>());

app.MapOpenApi();

app.MapGet("/", () => "Hello World!");

app.MapGet("/npcs/{id:long}", (long id) =>
{
    var result = NpcInspectionQuery.Inspect(worldHost.Current, new NpcId(id));
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
app.MapConversationEndpoints(worldHost, sessions, orchestrator);
app.MapNarrativeEndpoints(worldHost, chronicles);
app.MapPeriodsEndpoints();
app.MapWorldStartEndpoints();
app.MapWorldCreateEndpoints(worldHost, worldRunner, worldSink);
app.MapWorldPreviewEndpoints();
app.MapRealtimeEndpoints();
app.MapVisualInputEndpoints();

app.Run();

public partial class Program;
