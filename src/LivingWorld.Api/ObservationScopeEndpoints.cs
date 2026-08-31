using System.Collections.Concurrent;
using LivingWorld.Domain;
using LivingWorld.Simulation;
using LivingWorld.Simulation.Observation;

namespace LivingWorld.Api;

/// <summary>Espelha <c>SpaceId</c> do cliente — mesmo vocabulário, sem tradução (Fase 28, LOD-04).</summary>
public sealed record ObservationScopeDto(string Kind, string? CityId = null, string? BuildingId = null);

public sealed record ObservationScopeRequest(string SourceId, ObservationScopeDto? Scope);

/// <summary>Fase 28, T8 (LOD-04): traduz <c>POST /observation/scope</c> para
/// <see cref="ObservationRegistry.SetScope"/>/<see cref="ObservationRegistry.ClearScope"/> —
/// validação de borda na API; heartbeat com timeout configurável remove fontes stale.</summary>
public static class ObservationScopeEndpoints
{
    private static readonly ConcurrentDictionary<WorldHost, HeartbeatTracker> Trackers = new();

    public static void MapObservationScopeEndpoints(this WebApplication app, WorldHost worldHost)
    {
        app.MapPost("/observation/scope", (ObservationScopeRequest request, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.SourceId))
                return Results.BadRequest("sourceId: campo obrigatório ausente ou inválido");

            var timeout = TimeSpan.FromSeconds(
                configuration.GetValue("Observation:HeartbeatTimeoutSeconds", 30));

            var world = worldHost.Current;
            var registry = world.ObservationRegistry;
            var tracker = Trackers.GetOrAdd(worldHost, _ => new HeartbeatTracker());

            tracker.PurgeStale(registry, timeout);

            if (request.Scope is null)
            {
                registry.ClearScope(request.SourceId);
                tracker.Touch(request.SourceId);
                return Results.Ok();
            }

            var parsed = ParseScope(request.Scope, world);
            if (!parsed.IsSuccess)
                return Results.BadRequest(parsed.Error);

            registry.SetScope(request.SourceId, parsed.Value!);
            tracker.Touch(request.SourceId);
            return Results.Ok();
        });
    }

    internal static Result<SpaceScope> ParseScope(ObservationScopeDto dto, WorldState world)
    {
        if (string.IsNullOrWhiteSpace(dto.Kind))
            return Result<SpaceScope>.Fail("scope.kind: campo obrigatório ausente ou inválido");

        return dto.Kind switch
        {
            "World" => Result<SpaceScope>.Ok(SpaceScope.World()),
            "City" => ParseCityScope(dto, world),
            "Building" => ParseBuildingScope(dto, world),
            _ => Result<SpaceScope>.Fail($"scope.kind: valor '{dto.Kind}' inválido"),
        };
    }

    private static Result<SpaceScope> ParseCityScope(ObservationScopeDto dto, WorldState world)
    {
        if (string.IsNullOrWhiteSpace(dto.CityId))
            return Result<SpaceScope>.Fail("scope.cityId: campo obrigatório ausente ou inválido");

        if (!Guid.TryParse(dto.CityId, out var cityGuid))
            return Result<SpaceScope>.Fail($"scope.cityId: valor '{dto.CityId}' inválido");

        var cityId = new CityId(cityGuid);
        var city = world.FindCity(cityId);
        if (city is null || city.MergedIntoCityId is not null)
            return Result<SpaceScope>.Fail($"scope.cityId: cidade {cityId} não encontrada");

        return Result<SpaceScope>.Ok(SpaceScope.City(cityId));
    }

    private static Result<SpaceScope> ParseBuildingScope(ObservationScopeDto dto, WorldState world)
    {
        if (string.IsNullOrWhiteSpace(dto.CityId))
            return Result<SpaceScope>.Fail("scope.cityId: campo obrigatório ausente ou inválido");
        if (string.IsNullOrWhiteSpace(dto.BuildingId))
            return Result<SpaceScope>.Fail("scope.buildingId: campo obrigatório ausente ou inválido");

        if (!Guid.TryParse(dto.CityId, out var cityGuid))
            return Result<SpaceScope>.Fail($"scope.cityId: valor '{dto.CityId}' inválido");
        if (!long.TryParse(dto.BuildingId, out var buildingValue))
            return Result<SpaceScope>.Fail($"scope.buildingId: valor '{dto.BuildingId}' inválido");

        var cityId = new CityId(cityGuid);
        var buildingId = new BuildingId(buildingValue);

        var building = world.FindBuilding(buildingId);
        if (building is null)
            return Result<SpaceScope>.Fail($"scope.buildingId: prédio {buildingId} não encontrado");
        if (building.City != cityId)
            return Result<SpaceScope>.Fail($"scope.buildingId: prédio {buildingId} não pertence à cidade {cityId}");

        return Result<SpaceScope>.Ok(SpaceScope.Building(cityId, buildingId));
    }

    private sealed class HeartbeatTracker
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, DateTimeOffset> _lastHeartbeatBySource = new(StringComparer.Ordinal);

        public void Touch(string sourceId)
        {
            lock (_lock)
                _lastHeartbeatBySource[sourceId] = DateTimeOffset.UtcNow;
        }

        public void PurgeStale(ObservationRegistry registry, TimeSpan timeout)
        {
            var now = DateTimeOffset.UtcNow;
            List<string> stale;

            lock (_lock)
            {
                stale = _lastHeartbeatBySource
                    .Where(pair => now - pair.Value > timeout)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var sourceId in stale)
                    _lastHeartbeatBySource.Remove(sourceId);
            }

            foreach (var sourceId in stale)
                registry.ClearScope(sourceId);
        }
    }
}
