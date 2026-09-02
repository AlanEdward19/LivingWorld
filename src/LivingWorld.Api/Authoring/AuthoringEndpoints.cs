using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Authoring;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Authoring;

public sealed record PowerCommandRequest(string PowerId);
public sealed record InvokePowerRequest(
    string PowerId, long? TargetNpcId = null, CellCoord? TargetCell = null,
    ResolutionResult? Resolution = null);
public sealed record BreakRelationshipsRequest(long OtherNpcId);
public sealed record ForceActionRequest(ActionType Action);

public static class AuthoringEndpoints
{
    public static void MapAuthoringEndpoints(this WebApplication app)
    {
        app.MapGet("/authoring/extraordinary/catalog", (WorldHost host) =>
            Results.Ok(host.Current.Extraordinary.Descriptors));

        app.MapPost("/authoring/npcs/{id:long}/extraordinary/grant",
            (long id, PowerCommandRequest request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.Grant(host.Current, new NpcId(id), request.PowerId)));

        app.MapPost("/authoring/npcs/{id:long}/extraordinary/revoke",
            (long id, PowerCommandRequest request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.Revoke(host.Current, new NpcId(id), request.PowerId)));

        app.MapPost("/authoring/npcs/{id:long}/extraordinary/invoke",
            (long id, InvokePowerRequest request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.Invoke(
                    host.Current, new NpcId(id), request.PowerId,
                    new NpcId(request.TargetNpcId ?? id), request.TargetCell, request.Resolution)));

        app.MapPut("/authoring/npcs/{id:long}/personality",
            (long id, PersonalityValues request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.RewritePersonality(host.Current, new NpcId(id), request)));

        app.MapPost("/authoring/npcs/{id:long}/relationships/break",
            (long id, BreakRelationshipsRequest request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.BreakRelationships(host.Current, new NpcId(id), new NpcId(request.OtherNpcId))));

        app.MapPost("/authoring/npcs/{id:long}/action",
            (long id, ForceActionRequest request, WorldHost host, WorldAuthoringService commands) =>
                ResultOf(commands.ForceAction(host.Current, new NpcId(id), request.Action)));
    }

    private static IResult ResultOf<T>(Result<T> result) => result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.BadRequest(new { error = result.Error });
}
