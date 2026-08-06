using LivingWorld.AI;
using LivingWorld.Api;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Narrative;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SPEC_DEVIATION (Fase 8, T15, CITY-06): design.md pede carregar "o snapshot mais recente" via
// Infrastructure, mas hoje não existe nenhum snapshot persistido acessível a um host da API —
// a única persistência real (`persist-save`/`persist-resume` da Workers) fica atrás de um
// dbPath explícito passado por argumento de CLI, que a API não recebe. Monta um WorldState de
// cenário default (mesma seed usada em outros pontos do repo) só para prova de conceito do
// endpoint; ler o snapshot real de disco é infraestrutura nova, fora do escopo desta task.
// Fase 12, T7: `historyRules: HistoryRules.Default` liga a separação Crença/Verdade
// (HistoryBeliefQuery) que GET /narratives/reports precisa para calcular confiança — sem
// systems de Fase 10 registrados neste host (world nunca tica aqui, mesmo SPEC_DEVIATION
// acima), ligar a flag não muda nenhum comportamento dos endpoints já existentes.
var (world, _) = ScenarioRunner.Create(seed: 1, historyRules: HistoryRules.Default);

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
builder.Services.AddSingleton(sessions);

// Fase 13, T5: mesmo espírito do `world` acima — este host ainda não tem um dbPath real de
// disco (SPEC_DEVIATION de cima). Uma conexão sqlite `:memory:` mantida aberta pela vida do
// processo/factory guarda os templates de período enquanto o host roda; persistência real em
// disco fica para quando a API ganhar configuração de storage, fora do escopo desta task.
var periodTemplatesConnection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
periodTemplatesConnection.Open();
builder.Services.AddDbContext<WorldDbContext>(o => o.UseSqlite(periodTemplatesConnection));
builder.Services.AddScoped<IPeriodTemplateRepository, SqlitePeriodTemplateRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<WorldDbContext>().Database.Migrate();

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

app.Run();

public partial class Program;
