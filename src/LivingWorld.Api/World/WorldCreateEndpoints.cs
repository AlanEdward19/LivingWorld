using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.World;

public sealed record CreateWorldRequest(string ScenarioJson, string Name, string? PeriodId = null);

public sealed record CreateWorldResponse(int NpcCount, Guid WorldId, string Name, long Tick, string InitialScope);

/// <summary>Feature ad-hoc "criar mundo":
/// aceita um scenario JSON completo (mesmo formato de <see cref="LivingWorld.Simulation.Scenarios.ScenarioLoaderV2.LoadWorld"/>)
/// mais o nome escolhido pelo usuário, troca a instância canônica em <see cref="WorldHost"/> e
/// persiste imediatamente — sem isso o host nunca fica sem lastro no repositório entre o create
/// e o próximo snapshot automático.
/// AD-175, só acréscimos: <c>?startRunning=true</c> (com o token de admin) deixa o mundo rodando em vez de pausado, e
/// <c>POST /worlds/create/async</c> faz a mesma criação em segundo plano, com as etapas reais em
/// <c>GET /worlds/create/jobs/{jobId}</c>. Sem o parâmetro, <c>/worlds/create</c> responde como sempre (mundo pausado).</summary>
public static class WorldCreateEndpoints
{
    public static void MapWorldCreateEndpoints(this WebApplication app, WorldHost host, SavedWorldLibrary library, SimulationHost simulation)
    {
        app.MapPost("/worlds/create", (CreateWorldRequest request, bool? startRunning, HttpContext http, IConfiguration config,
            AdminAuditStore audit) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Name é obrigatório.");
            if (!WorldCreation.MayStart(startRunning, http, config, out var access))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var (status, error, response) = WorldCreation.Run(request, host, library, simulation);
            if (status == 400) return Results.BadRequest(error);
            if (status == 409) return Results.Conflict(error);
            if (access is not null) WorldCreation.Start(access, simulation, host, audit);
            return Results.Ok(response);
        });

        var jobs = new WorldCreationJobs();
        app.MapPost("/worlds/create/async", (CreateWorldRequest request, bool? startRunning, HttpContext http, IConfiguration config,
            IServiceScopeFactory scopes) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Results.BadRequest("Name é obrigatório.");
            if (!WorldCreation.MayStart(startRunning, http, config, out var access))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var job = jobs.Begin();
            _ = Task.Run(() =>
            {
                try
                {
                    var (status, error, response) = WorldCreation.Run(request, host, library, simulation, stage => jobs.Advance(job.JobId, stage));
                    bool running = false;
                    if (status == 200 && access is not null)
                    {
                        using var scope = scopes.CreateScope();
                        WorldCreation.Start(access, simulation, host, scope.ServiceProvider.GetRequiredService<AdminAuditStore>());
                        running = true;
                    }
                    jobs.Finish(job.JobId, status, error, response, running);
                }
                catch (Exception ex)
                {
                    jobs.Finish(job.JobId, 500, ex.Message, null, false);
                }
            });
            string statusUrl = $"/worlds/create/jobs/{job.JobId:D}";
            return Results.Accepted(statusUrl, new WorldCreationAccepted(job.JobId, statusUrl, WorldCreation.Stages));
        });

        app.MapGet("/worlds/create/jobs/{jobId:guid}", (Guid jobId) =>
            jobs.Find(jobId) is { } job ? Results.Ok(job) : Results.NotFound());
    }
}
