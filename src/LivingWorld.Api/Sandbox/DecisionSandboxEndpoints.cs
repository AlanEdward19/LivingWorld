using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Decision;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Sandbox;

public sealed record SandboxNeedsDto(int? Hunger = null, int? Thirst = null, int? Sleep = null, int? Social = null);

/// <summary>Corpo de <c>POST /npcs/{id}/decision-sandbox</c>. Tudo opcional; necessidades 0–100 (alto = satisfeito).</summary>
public sealed record DecisionSandboxRequestDto(
    SandboxNeedsDto? Needs = null, Dictionary<string, int>? Personality = null, ActionType? ContinuityAction = null);

public sealed record DecisionSandboxResponse(ActionType Action, DecisionTrace Trace);

/// <summary>"E se…?": roda o motor de decisão de um NPC real com estímulos sobrepostos e devolve a decisão e o
/// rastro (mesma forma de <c>cognitionTrace[].trace</c>). Nunca escreve no mundo (ADR-0026).</summary>
public static class DecisionSandboxEndpoints
{
    public static void MapDecisionSandboxEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapPost("/npcs/{id:long}/decision-sandbox", (long id, DecisionSandboxRequestDto? body) =>
        {
            body ??= new DecisionSandboxRequestDto();
            if (body.ContinuityAction is { } action && !Enum.IsDefined(action))
                return Results.BadRequest(new { error = "continuityAction: valor inválido" });

            var needs = body.Needs ?? new SandboxNeedsDto();
            var overrides = new NpcSandboxOverrides(
                needs.Hunger, needs.Thirst, needs.Sleep, needs.Social, body.Personality, body.ContinuityAction);
            var result = NpcDecisionSandbox.Run(host.Current, new NpcId(id), overrides);
            if (result.IsSuccess)
                return Results.Ok(new DecisionSandboxResponse(result.Value!.Action, result.Value.Trace));

            return result.Error!.StartsWith("NpcId", StringComparison.Ordinal)
                ? Results.NotFound()
                : Results.BadRequest(new { error = result.Error });
        });
    }
}
