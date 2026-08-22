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
    string Name,
    CellCoord Location,
    AggregatePopulationPool AggregatePool,
    IReadOnlyList<CityResidentMarker> Residents,
    // T50: ids reservados (City.PoolNpcIds) de membros do pool agregado ainda não materializados
    // — cliente desenha um token clicável por id, clique dispara MaterializeAndInspect.
    IReadOnlyList<NpcId> PendingResidentIds,
    IReadOnlyList<CityBuildingMarker> Buildings,
    IReadOnlyDictionary<VisualLayerId, LayerBuildResult> Layers,
    IReadOnlyList<SpatialPortal> Portals,
    CityIndicators Indicators,
    LivingScopeState LivingState,
    CellBounds Bounds,
    bool BoundsAreDerived);

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

        // dynamic-city-growth, T3: bounds resolvidos antes dos marcadores de prédio -- Resolve
        // agora precisa deles pra tentar uma célula livre dentro dos bounds antes de cair no
        // overflow (CITYGROW-01/02).
        long populationForBounds = CityPopulationQuery.Population(world, cityId);
        var (boundsForPlacement, boundsAreDerived) = SpatialBoundsResolver.ResolveCity(
            city, populationForBounds, world.Map.Width, world.Map.Height);

        var buildings = world.Buildings
            .Where(b => b.City == cityId)
            .Select(b =>
            {
                var (position, _, isDerived) = BuildingPlacementResolver.Resolve(b, city, world, boundsForPlacement);
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
            populationForBounds,
            CityPopulationQuery.Wealth(world, cityId),
            CityPopulationQuery.Health(world, cityId),
            CityPopulationQuery.Inequality(world, cityId),
            CityPopulationQuery.Economy(world, cityId),
            CityPopulationQuery.Housing(world, cityId));

        // Mesma fonte do marcador no mapa-múndi (GlobalProjector) — sem isso o envelope visual
        // de dentro da cidade (CityView) usava um tamanho fixo (16x16, cityGroundBounds.ts)
        // desconectado do footprint real, que já cresce com a população (LIVE-POLISH: usuário
        // reportou cidade "3x3" no mundo mas outro tamanho dentro dela). Mesmos bounds já
        // resolvidos acima para os marcadores de prédio (T3) — nunca recalculado duas vezes.
        var cellBounds = new CellBounds(
            boundsForPlacement.Origin.X, boundsForPlacement.Origin.Y, boundsForPlacement.Width, boundsForPlacement.Height);

        var livingState = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.City, city.Id.ToString()));
        return Result<CitySnapshot>.Ok(new CitySnapshot(
            city.Id, city.Name, city.Location, city.AggregatePool, residents, city.PoolNpcIds.ToList(), buildings,
            layers, portals, indicators, livingState, cellBounds, boundsAreDerived));
    }

    private static bool TouchesCity(PortalEndpoint endpoint, string cityRefId) =>
        endpoint.Space == PortalSpaceKind.City && endpoint.RefId == cityRefId;
}
