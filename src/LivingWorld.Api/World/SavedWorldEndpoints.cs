using LivingWorld.Api.Periods;
using LivingWorld.Infrastructure.Repositories;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.World;

public sealed record RenameWorldRequest(string Name);

public sealed record SaveWorldResponse(Guid WorldId, string Name, long Tick, DateTimeOffset SavedAtUtc);

/// <summary>Mundos salvos (o menu "Meus mundos"). AD-175, só acréscimos: a lista traz período, ano, quando foi jogado, se é o
/// mundo aberto e a origem; <c>?startRunning=true</c> no carregar (com token de admin, como <c>/simulation/resume</c>) deixa
/// o mundo rodando; <c>POST /worlds/{id}/rename</c> renomeia; <c>POST /world/save</c> grava o mundo aberto agora.
/// Renomear e salvar seguem a proteção das rotas vizinhas (<c>/worlds/create</c>, carregar e apagar não pedem token).</summary>
public static class SavedWorldEndpoints
{
    public const int MaxNameLength = 40;

    public static void MapSavedWorldEndpoints(this WebApplication app, WorldHost host, SavedWorldLibrary library,
        SimulationHost simulation, Func<WorldState, WorldClock> clockFor)
    {
        app.MapGet("/worlds", (IPeriodTemplateRepository periods) => Results.Ok(library.Summaries(PeriodNames(periods))));

        app.MapPost("/worlds/{worldId:guid}/load", (Guid worldId, bool? startRunning, HttpContext http, IConfiguration config,
            AdminAuditStore audit) =>
        {
            if (!WorldCreation.MayStart(startRunning, http, config, out var access))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!library.Load(worldId, host, simulation, clockFor)) return Results.NotFound();
            if (access is not null) WorldCreation.Start(access, simulation, host, audit);
            return Results.Ok();
        });

        app.MapDelete("/worlds/{worldId:guid}", (Guid worldId) => Results.StatusCode(library.Delete(worldId)));

        app.MapPost("/worlds/{worldId:guid}/rename", (Guid worldId, RenameWorldRequest request, IPeriodTemplateRepository periods) =>
        {
            string name = (request.Name ?? "").Trim();
            if (name.Length == 0)
                return Results.BadRequest(new { error = "O nome não pode ficar vazio." });
            if (name.Length > MaxNameLength)
                return Results.BadRequest(new { error = $"O nome pode ter no máximo {MaxNameLength} letras." });
            if (name.Any(char.IsControl))
                return Results.BadRequest(new { error = "O nome não pode ter quebra de linha nem caractere de controle." });
            return library.Rename(worldId, name, host) switch
            {
                RenameOutcome.NotFound => Results.NotFound(new { error = "Este mundo não existe mais." }),
                RenameOutcome.Duplicate => Results.Conflict(new { error = $"Já existe um mundo chamado “{name}”." }),
                _ => Results.Ok(library.Summary(worldId, PeriodNames(periods))),
            };
        });

        app.MapPost("/world/save", () =>
        {
            var saved = library.SaveNow(host.Current);
            return Results.Ok(new SaveWorldResponse(saved.WorldId, saved.Name, saved.CurrentDate, saved.LastSavedAtUtc));
        });
    }

    /// <summary>Nome do cartão do período (<see cref="PeriodCards.NameOf"/>); id sem cadastro fica com o próprio id.</summary>
    private static Func<string, string> PeriodNames(IPeriodTemplateRepository periods) => id =>
        periods.FindLatestVersion(id) is { } template ? PeriodCards.NameOf(template.PeriodId, template.Source, template.PayloadJson) : id;
}
