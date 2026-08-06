using LivingWorld.Api.Realtime;
using LivingWorld.Api.Visual;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Visibility;

namespace LivingWorld.Api.VisualInput;

/// <summary>Fase 15, T7 (spec.md story "Modo personagem com FOW", AC1): intenção de movimento —
/// só <c>/move</c> tem critério de aceite na spec (click/WASD, validação server-side, delta
/// publicado). <c>/interact</c> do design.md não tem nenhum AC que defina comportamento
/// esperado — implementá-lo agora seria um "what if" sem lastro em spec (Check C de tasks.md);
/// fica deferido até a spec definir o que "interagir" significa.</summary>
public sealed record PlayerMoveRequest(int TargetX, int TargetY, string InputMode);

public static class VisualInputEndpoints
{
    public static void MapVisualInputEndpoints(this WebApplication app)
    {
        app.MapPost("/visual/player/{id:long}/move", (long id, PlayerMoveRequest request, WorldState world, RealtimeGateway gateway) =>
        {
            var npc = world.Npcs.FirstOrDefault(n => n.Id == new NpcId(id));
            if (npc is null) return Results.NotFound();

            var target = new CellCoord(request.TargetX, request.TargetY);
            var validation = PlayerMovementValidator.Validate(world, npc, target);
            if (!validation.IsSuccess) return Results.BadRequest(validation.Error);

            npc.MoveTo(target, world.CurrentDate.TotalHours);

            var cityScope = new VisualScope(VisualScopeKind.City, npc.City.Value.ToString());
            gateway.Publish(cityScope, new { NpcId = npc.Id, Location = target });

            return Results.Ok();
        });
    }
}
