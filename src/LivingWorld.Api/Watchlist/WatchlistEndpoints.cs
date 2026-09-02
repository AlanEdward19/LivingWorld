using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.Watchlist;

/// <summary>Fase 28 T7 (COG-20, COG-21): marca/desmarca NPC na watchlist de
/// <see cref="NpcCognitionLog"/> — rejeita morto ou arquivado na borda.</summary>
public static class WatchlistEndpoints
{
    public static void MapWatchlistEndpoints(this WebApplication app, WorldHost host)
    {
        app.MapPost("/npcs/{id:long}/watchlist", (long id) =>
            ResultOf(MarkWatchlisted(host.Current, new NpcId(id))));

        app.MapDelete("/npcs/{id:long}/watchlist", (long id) =>
            ResultOf(Unmark(host.Current, new NpcId(id))));
    }

    private static Result<Unit> MarkWatchlisted(WorldState world, NpcId id)
    {
        var validation = ValidateLivingNpc(world, id);
        if (!validation.IsSuccess)
            return validation;

        world.CognitionLog.MarkWatchlisted(id, world.CurrentDate.TotalHours);
        return Result<Unit>.Ok(Unit.Value);
    }

    private static Result<Unit> Unmark(WorldState world, NpcId id)
    {
        var validation = ValidateLivingNpc(world, id);
        if (!validation.IsSuccess)
            return validation;

        world.CognitionLog.Unmark(id);
        return Result<Unit>.Ok(Unit.Value);
    }

    private static Result<Unit> ValidateLivingNpc(WorldState world, NpcId id)
    {
        if (world.ColdArchive.Lookup(id.Value) is not null)
            return Result<Unit>.Fail("NpcId: NPC arquivado");

        var npc = world.FindNpc(id);
        if (npc is null || !npc.IsAlive)
            return Result<Unit>.Fail("NpcId: NPC ausente ou morto");

        return Result<Unit>.Ok(Unit.Value);
    }

    private static IResult ResultOf(Result<Unit> result) => result.IsSuccess
        ? Results.Ok()
        : Results.BadRequest(new { error = result.Error });
}
