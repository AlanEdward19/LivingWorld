using LivingWorld.AI;
using LivingWorld.Api;
using LivingWorld.Api.Realtime;
using LivingWorld.Api.Simulation;
using LivingWorld.Api.VisualInput;
using LivingWorld.Domain;
using LivingWorld.Domain.Llm;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Fase 15, T9: documento OpenAPI (/openapi/v1.json) — fonte pros tipos TS do cliente web,
// gerados por scripts/generate-web-types.sh a partir deste endpoint.
builder.Services.AddOpenApi();

// Testes continuam isolados em memória quando não configuram conexão. O app iniciado por
// run.cmd injeta um SQLite em disco, portanto o último mundo sobrevive ao restart da API.
var worldConnectionString = builder.Configuration.GetConnectionString("World") ?? "Data Source=:memory:";
var worldDbConnection = new Microsoft.Data.Sqlite.SqliteConnection(worldConnectionString);
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

// Sessões e crônicas vivem em memória, mas a instância consultada pelos endpoints precisa ser
// a mesma dirigida pelo relógio para eventos agendados e publicações ficarem observáveis.
var sessions = new ConversationSessionStore();
var chronicles = new ChronicleGenerationSystem();
var world = worldRunner.LoadLatest();
if (world is null)
{
    // 20, não `DefaultInitialPopulation` (100, usado pelos testes) — no mapa 10x10 padrão do
    // bootstrap, 100 residentes todos materializados excede o footprint de cidade compacto
    // (CityBoundsResolver trava em metade do mapa = 5x5 = 25 células aqui), empilhando várias
    // famílias na mesma célula (LIVE-POLISH). 20 casa com o preset "Pequeno" do World Creator
    // pro mesmo tamanho de mapa.
    (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 20, historyRules: HistoryRules.Default);

    // Bugfix real (usuário, 2026-08-15): o comentário acima sempre disse "já persiste", mas
    // nada chamava Snapshot aqui — só `PersistentWorldRunner.Run` salva, e só a cada 24 ticks
    // (`snapshotIntervalTicks`). Um mundo recém-criado, reiniciado antes do tick 24 (ou nunca
    // tickado, TICK_LOOP_ENABLED=false), perdia o "canônico compartilhado" que T2 prometia —
    // reiniciar sorteava outro mundo do zero. Salva o snapshot inicial imediatamente.
    worldRunner.Snapshot(world, worldSink);
}

// Fase 16, T2: snapshot extraordinário ligado precisa reconstruir o mesmo registro seletivo;
// o relógio não pode ser composto antes de sabermos qual mundo persistido foi carregado.
var worldClock = new WorldClock(
    ScenarioRunner.DefaultSystems(
        conversationSessions: sessions, chronicles: chronicles, extraordinary: world.Extraordinary),
    sink: worldSink);

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
builder.Services.AddSingleton(chronicles);

// Fase 15.1, T3 (VTT2-26): registrado sempre (resolvível/testável direto via TickLoopService),
// mas só roda sozinho como IHostedService com TICK_LOOP_ENABLED=true — desabilitado por default
// pra nenhuma WebApplicationFactory de teste existente ganhar um mundo mudando sozinho embaixo
// dela (mesmo motivo documentado em WorldHost/Program.cs:71-74).
builder.Services.AddSingleton<TickLoopService>();
if (builder.Configuration["TICK_LOOP_ENABLED"] == "true")
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TickLoopService>());

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

// Fase 15.1, T49 (backend-gaps.md G9): comando explícito e nomeado — o GET acima nunca
// materializa; quem precisa do detalhe completo de um id ainda anônimo no pool agregado chama
// esta rota de propósito.
app.MapPost("/npcs/{id:long}/materialize", (long id) =>
{
    var result = NpcInspectionQuery.MaterializeAndInspect(worldHost.Current, new NpcId(id));
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
app.MapSimulationControlEndpoints();
app.MapRealtimeEndpoints();
app.MapVisualInputEndpoints();

app.Run();

public partial class Program;
