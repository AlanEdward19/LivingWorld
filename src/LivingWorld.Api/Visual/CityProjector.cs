using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15, T5 (VTT-03, VTT-11): morador materializado no foco de cidade — posição e
/// atividade atual, mesmo par de campos de <c>NpcInspectionDto</c> relevante pro mapa, sem o
/// resto do detalhe de inspeção individual.</summary>
public sealed record CityResidentMarker(NpcId Id, CellCoord Location, ActionType? CurrentAction);

/// <summary><see cref="Location"/>/<see cref="LocationIsDerived"/> (Fase 15.1, T20, OQ-1) vêm de
/// <see cref="BuildingPlacementResolver.Resolve"/> — autoria tem precedência; sem ela, fallback
/// determinístico e estável por <see cref="BuildingId"/>, nunca a posição derivada do índice de
/// iteração que o anel client-side usa hoje.</summary>
public sealed record CityBuildingMarker(BuildingId Id, int BuildingTypeId, CellCoord Location, bool LocationIsDerived);

/// <summary>Fase 15.1, T30 (spec.md "Inspector de NPC e Cidade" AC1): os 6 indicadores que <see
/// cref="CityPopulationQuery"/> já calcula, hoje inacessíveis ao cliente. <see
/// cref="CityPopulationQuery"/> é a única fonte — nenhum indicador é recomputado aqui.</summary>
public sealed record CityIndicators(long Population, long Wealth, long Health, double Inequality, long Economy, long Housing);

public sealed record CitySnapshot(
    CityId Id,
    CellCoord Location,
    AggregatePopulationPool AggregatePool,
    IReadOnlyList<CityResidentMarker> Residents,
    IReadOnlyList<CityBuildingMarker> Buildings,
    IReadOnlyDictionary<VisualLayerId, LayerBuildResult> Layers,
    IReadOnlyList<SpatialPortal> Portals,
    CityIndicators Indicators,
    LivingScopeState LivingState);

public static class CityProjector
{
    public static Result<CitySnapshot> Build(WorldState world, CityId cityId)
    {
        var city = world.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null) return Result<CitySnapshot>.Fail($"cidade {cityId} não encontrada");

        var residents = world.Npcs
            .Where(n => n.IsAlive && n.City == cityId)
            .Select(n => new CityResidentMarker(n.Id, n.CurrentLocation, n.CurrentAction))
            .ToList();

        var buildings = world.Buildings
            .Where(b => b.City == cityId)
            .Select(b =>
            {
                var (position, _, isDerived) = BuildingPlacementResolver.Resolve(b, city);
                return new CityBuildingMarker(b.Id, b.BuildingTypeId, position, isDerived);
            })
            .ToList();

        var layers = CityLayerBuilder.SupportedLayers.ToDictionary(id => id, CityLayerBuilder.Build);
        layers[VisualLayerId.Climate] = GlobalLayerBuilder.Build(VisualLayerId.Climate, world);

        // Fase 15.1, T21: portal cujo From ou To referencia esta cidade — mesma semântica de
        // MockPortalSource.portalsOf (web/src/data/mock/MockPortalSource.ts).
        string cityRefId = city.Id.ToString();
        var portals = world.Portals
            .Where(p => TouchesCity(p.From, cityRefId) || TouchesCity(p.To, cityRefId))
            .ToList();

        var indicators = new CityIndicators(
            CityPopulationQuery.Population(world, cityId),
            CityPopulationQuery.Wealth(world, cityId),
            CityPopulationQuery.Health(world, cityId),
            CityPopulationQuery.Inequality(world, cityId),
            CityPopulationQuery.Economy(world, cityId),
            CityPopulationQuery.Housing(world, cityId));

        var livingState = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        return Result<CitySnapshot>.Ok(new CitySnapshot(city.Id, city.Location, city.AggregatePool, residents, buildings, layers, portals, indicators, livingState));
    }

    private static bool TouchesCity(PortalEndpoint endpoint, string cityRefId) =>
        endpoint.Space == PortalSpaceKind.City && endpoint.RefId == cityRefId;
}
