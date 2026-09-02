using LivingWorld.Domain.Geography.Map;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Geography;
using LivingWorld.Simulation.Hosting;

namespace LivingWorld.Api.World;

public sealed record PreviewWorldRequest(string ScenarioJson);

public sealed record PreviewCell(int X, int Y, int Terrain, int Biome, int Altitude, bool HasWater, IReadOnlyList<int> Resources);

public sealed record PreviewSettlement(string Name, int X, int Y);

public sealed record PreviewWorldResponse(
    int Width, int Height, IReadOnlyList<PreviewCell> Cells, IReadOnlyList<PreviewSettlement> Settlements, string SpatialHash);

/// <summary>Preview canônico de cenário (Fase 15.1, T43/backend-gaps.md G2): usa o mesmo
/// <see cref="MapScenarioLoader"/> que <c>ScenarioLoaderV2.LoadWorld</c> (via
/// <c>PeriodDefinitionValidator</c>) consome no create — sem isso o World Creator mostra uma
/// aproximação client-side (<c>creatorWorldVisuals.ts</c>) desconectada do mapa real que
/// <c>POST /worlds/create</c> vai produzir para a mesma seed. Read-only: não constrói
/// <see cref="WorldState"/>, não toca <see cref="WorldHost"/>, não persiste nada.</summary>
public static class WorldPreviewEndpoints
{
    public static void MapWorldPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/worlds/preview", (PreviewWorldRequest request) =>
        {
            var result = MapScenarioLoader.Load(request.ScenarioJson);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);

            var map = result.Value!;
            var cells = map.Cells
                .Select(c => new PreviewCell(
                    c.Coord.X, c.Coord.Y, c.Terrain.Id, c.Biome.Id, c.Altitude, c.HasWater,
                    c.Resources.Select(r => r.Id).ToArray()))
                .ToArray();
            var settlements = map.Settlements
                .Select(s => new PreviewSettlement(s.Name, s.Cell.X, s.Cell.Y))
                .ToArray();

            return Results.Ok(new PreviewWorldResponse(map.Width, map.Height, cells, settlements, MapSpatialHash.Compute(map)));
        });
    }
}
