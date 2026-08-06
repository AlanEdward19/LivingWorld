using System.Text.Json;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation.Periods;

namespace LivingWorld.Api;

public sealed record CreatePeriodRequest(string PeriodId, int Version, JsonElement PeriodDefinition, string Source);

public sealed record PeriodSummaryResponse(string PeriodId, int Version, string Source, DateTime CreatedAtUtc);

public sealed record PeriodDetailResponse(string PeriodId, int Version, string Source, DateTime CreatedAtUtc, JsonElement PeriodDefinition);

/// <summary>Fase 13, T12 (PERIOD-22..23): catálogo de ids ativos de um período registrado —
/// nunca nome, só id (AD-023/AD-025).</summary>
public sealed record PeriodCatalogResponse(
    string PeriodId, int Version, IReadOnlyList<int> ProfessionIds, IReadOnlyList<int> SkillIds);

/// <summary>Fase 13, T5 (PERIOD-07..10, story "Cadastro de período personalizado"): <c>POST
/// /periods</c> valida (<see cref="PeriodDefinitionValidator"/>) e persiste (<see
/// cref="IPeriodTemplateRepository"/>) um período; <c>GET /periods</c>/<c>GET /periods/{id}</c>
/// leem o catálogo registrado. Mesmo padrão de <see cref="ConversationEndpoints"/> — tradução
/// request/response e os 400/404/409 que a spec pede, nenhuma regra nova aqui.</summary>
public static class PeriodsEndpoints
{
    public static void MapPeriodsEndpoints(this WebApplication app)
    {
        app.MapPost("/periods", (CreatePeriodRequest request, IPeriodTemplateRepository repository) =>
        {
            string payloadJson = request.PeriodDefinition.GetRawText();

            var validation = PeriodDefinitionValidator.Validate(payloadJson);
            if (!validation.IsSuccess) return Results.BadRequest(validation.Error);

            var createdAtUtc = DateTime.UtcNow;
            var saveResult = repository.Save(new PeriodTemplateRecord
            {
                PeriodId = request.PeriodId,
                Version = request.Version,
                PayloadJson = payloadJson,
                CreatedAtUtc = createdAtUtc,
                Source = request.Source,
            });
            if (!saveResult.IsSuccess) return Results.Conflict(saveResult.Error);

            return Results.Created(
                $"/periods/{request.PeriodId}",
                new PeriodSummaryResponse(request.PeriodId, request.Version, request.Source, createdAtUtc));
        });

        app.MapGet("/periods", (IPeriodTemplateRepository repository) =>
        {
            var catalog = repository.ListLatestPerPeriod()
                .Select(t => new PeriodSummaryResponse(t.PeriodId, t.Version, t.Source, t.CreatedAtUtc));
            return Results.Ok(catalog);
        });

        app.MapGet("/periods/{id}", (string id, IPeriodTemplateRepository repository) =>
        {
            var template = repository.FindLatestVersion(id);
            if (template is null) return Results.NotFound();

            return Results.Ok(new PeriodDetailResponse(
                template.PeriodId, template.Version, template.Source, template.CreatedAtUtc,
                JsonDocument.Parse(template.PayloadJson).RootElement));
        });

        app.MapGet("/periods/{id}/catalog", (string id, IPeriodTemplateRepository repository) =>
        {
            var template = repository.FindLatestVersion(id);
            if (template is null) return Results.NotFound();

            // Já validado no cadastro (POST /periods) — Validate aqui só reconstrói o
            // PeriodDefinition a partir do payload persistido, nunca deveria falhar.
            var definition = PeriodDefinitionValidator.Validate(template.PayloadJson).Value!;
            var catalog = PeriodCatalog.From(definition);

            return Results.Ok(new PeriodCatalogResponse(
                template.PeriodId, template.Version, catalog.ProfessionIds, catalog.SkillIds));
        });
    }
}
