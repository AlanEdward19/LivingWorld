using LivingWorld.Domain;
using LivingWorld.Domain.Narrative;
using LivingWorld.Simulation;
using LivingWorld.Simulation.History;
using LivingWorld.Simulation.Narrative;

namespace LivingWorld.Api;

public sealed record NarrativeClaimDto(string Text, IReadOnlyList<long> EventIds);

public sealed record ChronicleResponse(long Id, string Prose, IReadOnlyList<NarrativeClaimDto> Claims);

public sealed record BiographyResponse(long NpcId, string Prose, IReadOnlyList<NarrativeClaimDto> Claims);

public sealed record ReportResponse(long ReportId, long OriginFactId, string Medium, double Confidence);

/// <summary>Fase 12, T7 (NARR-19..21, story "Endpoints de leitura narrativa"): liga os endpoints
/// HTTP ao pipeline já pronto (<see cref="WindowedHistoryAggregator"/>/<see cref="ChronicleGenerationSystem"/>,
/// <see cref="NpcBiographyQuery"/>, <see cref="NarrativeRenderer"/>, <see cref="HistoryBeliefQuery"/>)
/// — mesmo padrão de <see cref="ConversationEndpoints"/>: nenhuma lógica de decisão nova aqui, só
/// tradução request/response. Todas as consultas passam por crença (<see cref="HistoryBeliefQuery"/>),
/// nunca por <see cref="HistoryTruthQuery"/> — jogo só lê crença (NARR-15, rules/llm-boundary.md).</summary>
public static class NarrativeEndpoints
{
    public static void MapNarrativeEndpoints(this WebApplication app, WorldHost host, ChronicleGenerationSystem chronicles)
    {
        app.MapGet("/narratives/chronicles", (Guid? location, long? periodStart, long? periodEnd, int? topK) =>
        {
            if (periodStart is null || periodEnd is null)
                return Results.BadRequest("periodStart e periodEnd são obrigatórios");

            CityId? cityId = location is { } value ? new CityId(value) : null;
            var document = chronicles.GenerateChronicle(host.Current, cityId, periodStart.Value, periodEnd.Value, topK ?? 5);
            return Results.Ok(ToChronicleResponse(document));
        });

        app.MapGet("/narratives/biographies/{npcId:long}", async (long npcId) =>
        {
            var timeline = NpcBiographyQuery.Timeline(host.Current, new NpcId(npcId));
            if (!timeline.IsSuccess) return Results.NotFound();

            var claims = timeline.Value!
                .Select(f => new NarrativeClaim($"{f.Kind} (evento {f.Id.Value}): {f.Payload}", (IReadOnlyList<long>)[f.Id.Value]))
                .ToList();
            long periodStart = timeline.Value!.Count > 0 ? timeline.Value[0].Tick : 0;
            long periodEnd = timeline.Value.Count > 0 ? timeline.Value[^1].Tick + 1 : 0;
            var draft = new NarrativeDraft(null, periodStart, periodEnd, claims);

            var document = await NarrativeRenderer.RenderAsync(new NarrativeId(npcId), NarrativeType.Biography, draft);
            return Results.Ok(new BiographyResponse(npcId, document.Prose, document.Claims.Select(ToClaimDto).ToList()));
        });

        app.MapGet("/narratives/reports", () =>
        {
            var world = host.Current;
            var responses = world.Reports.Select(report =>
            {
                var belief = HistoryBeliefQuery.BeliefOf(world, report.CommunityId, report.OriginFactId);
                double confidence = belief.IsSuccess ? Math.Clamp(1.0 - belief.Value!.DistanceFromFact, 0.0, 1.0) : 0.0;
                return new ReportResponse(report.Id.Value, report.OriginFactId.Value, report.Medium.ToString(), confidence);
            }).ToList();
            return Results.Ok(responses);
        });
    }

    private static ChronicleResponse ToChronicleResponse(NarrativeDocument document) =>
        new(document.Id.Value, document.Prose, document.Claims.Select(ToClaimDto).ToList());

    private static NarrativeClaimDto ToClaimDto(NarrativeClaim claim) => new(claim.Text, claim.EventIds);
}
