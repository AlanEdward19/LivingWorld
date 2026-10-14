using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.History.Queries;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Narrative;

public sealed record BeliefResponse(long ReportId, double DistortedMagnitude, long DistortedTick, string Seed, double DistanceFromFact);

/// <summary><c>GET /history/truth</c> (só com escopo de motor: header <c>X-Engine-Scope</c> igual a
/// <c>HISTORY_ENGINE_SCOPE_TOKEN</c>, senão 403) e <c>GET /history/beliefs?communityId=</c> (relatos vivos
/// do cânone da comunidade, 404 se ela não existe). O jogo só lê crença — rules/llm-boundary.md.</summary>
public static class HistoryEndpoints
{
    public const string ScopeHeader = "X-Engine-Scope";

    public static void MapHistoryEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapGet("/history/truth", (HttpContext http, IConfiguration config, string? scope, long? fromTick) =>
        {
            string? presented = http.Request.Headers[ScopeHeader].FirstOrDefault();
            var grant = EngineScope.TryGrant(presented, config["HISTORY_ENGINE_SCOPE_TOKEN"]);
            var facts = HistoryTruthGateway.Skeleton(host.Current, grant, fromTick ?? 0);
            return facts.IsSuccess ? Results.Ok(facts.Value) : Results.StatusCode(StatusCodes.Status403Forbidden);
        });

        app.MapGet("/history/beliefs", (Guid? communityId) =>
        {
            if (communityId is null) return Results.BadRequest("communityId é obrigatório");
            var world = host.Current;
            var city = world.FindCity(new CityId(communityId.Value));
            if (city is null) return Results.NotFound();

            var beliefs = new List<BeliefResponse>();
            foreach (var slot in city.CanonSlots.OrderBy(s => s.Id.Value))
            {
                var belief = HistoryBeliefQuery.BeliefOf(world, city.Id, slot.OriginFactId);
                if (!belief.IsSuccess) continue;
                var b = belief.Value!;
                beliefs.Add(new BeliefResponse(
                    b.ReportId.Value, b.DistortedMagnitude, b.DistortedTick, b.MoralizedNarrativeSeed, b.DistanceFromFact));
            }
            return Results.Ok(beliefs);
        });
    }
}
