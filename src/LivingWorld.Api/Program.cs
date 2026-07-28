using LivingWorld.Domain;
using LivingWorld.Simulation;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

// SPEC_DEVIATION (Fase 8, T15, CITY-06): design.md pede carregar "o snapshot mais recente" via
// Infrastructure, mas hoje não existe nenhum snapshot persistido acessível a um host da API —
// a única persistência real (`persist-save`/`persist-resume` da Workers) fica atrás de um
// dbPath explícito passado por argumento de CLI, que a API não recebe. Monta um WorldState de
// cenário default (mesma seed usada em outros pontos do repo) só para prova de conceito do
// endpoint; ler o snapshot real de disco é infraestrutura nova, fora do escopo desta task.
var (world, _) = ScenarioRunner.Create(seed: 1);

app.MapGet("/npcs/{id:long}", (long id) =>
{
    var result = NpcInspectionQuery.Inspect(world, new NpcId(id));
    return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound();
});

app.Run();

public partial class Program;
