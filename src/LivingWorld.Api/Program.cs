using LivingWorld.AI;
using LivingWorld.Api;
using LivingWorld.Domain.Llm;
using LivingWorld.Domain;
using LivingWorld.Simulation;

var builder = WebApplication.CreateBuilder(args);

// SPEC_DEVIATION (Fase 8, T15, CITY-06): design.md pede carregar "o snapshot mais recente" via
// Infrastructure, mas hoje não existe nenhum snapshot persistido acessível a um host da API —
// a única persistência real (`persist-save`/`persist-resume` da Workers) fica atrás de um
// dbPath explícito passado por argumento de CLI, que a API não recebe. Monta um WorldState de
// cenário default (mesma seed usada em outros pontos do repo) só para prova de conceito do
// endpoint; ler o snapshot real de disco é infraestrutura nova, fora do escopo desta task.
var (world, _) = ScenarioRunner.Create(seed: 1);

// Fase 11, T7: sessão/efeitos vivem só em memória do processo (mesmo espírito do `world`
// acima) — nunca fazem parte do snapshot/hash canônico do mundo.
var sessions = new ConversationSessionStore();

// Registrados no DI (em vez de campos `static` em `Program`) para que cada instância de
// `WebApplicationFactory<Program>` (uma por classe de teste) tenha seu próprio `world`/
// `sessions` isolado — campos `static` eram compartilhados entre TODAS as factories do
// processo e colidiam quando classes de teste rodavam em paralelo (xUnit default).
builder.Services.AddSingleton(world);
builder.Services.AddSingleton(sessions);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("/npcs/{id:long}", (long id) =>
{
    var result = NpcInspectionQuery.Inspect(world, new NpcId(id));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
});

var effects = new ConversationEffectsApplier();
var orchestrator = new ConversationOrchestrator(
    sessions, effects, new FakeLlmProvider(),
    knownEmotions: ["neutral", "concerned", "happy", "annoyed", "curious", "afraid"],
    budgetPerInteraction: TimeSpan.FromSeconds(5));
app.MapConversationEndpoints(world, sessions, orchestrator);

app.Run();

public partial class Program;
