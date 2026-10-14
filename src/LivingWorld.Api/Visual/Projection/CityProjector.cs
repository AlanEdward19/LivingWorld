using LivingWorld.Api.Visual.Catalogs;
using LivingWorld.Api.Visual.Layers;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Morador materializado no foco de cidade — posição e
/// atividade atual, mesmo par de campos de <c>NpcInspectionDto</c> relevante pro mapa, sem o
/// resto do detalhe de inspeção individual.</summary>
public sealed record CityResidentMarker(
    NpcId Id, CellCoord Location, ActionType? CurrentAction, ExtraordinaryNpcVisual? Extraordinary = null);

/// <summary><see cref="Location"/>/<see cref="LocationIsDerived"/> vêm de
/// <see cref="BuildingPlacementResolver.Resolve"/> — autoria tem precedência; sem ela, fallback
/// determinístico e estável por <see cref="BuildingId"/>, nunca a posição derivada do índice de
/// iteração que o anel client-side usa hoje.</summary>
public sealed record CityBuildingMarker(
    BuildingId Id, int BuildingTypeId, CellCoord Location, bool LocationIsDerived, int Orientation = 0,
    int Floors = 1, IReadOnlyList<FootprintCell>? GroundFootprint = null, long Value = 0, LodgingMarker? Lodging = null);

/// <summary>Hospedagem de um prédio (lodging-and-rent): vagas, vagas ocupadas por domicílios, aluguel mensal e diária de hoje —
/// o preço já acompanha local e demanda. Só existe em prédio cujo tipo declara <c>Lodging</c>.</summary>
public sealed record LodgingMarker(int Units, int Occupied, long RentPerMonth, long NightlyRate);

/// <summary>Os 6 indicadores que <see
/// cref="CityPopulationQuery"/> já calcula, hoje inacessíveis ao cliente. <see
/// cref="CityPopulationQuery"/> é a única fonte — nenhum indicador é recomputado aqui.</summary>
public sealed record CityIndicators(long Population, long Wealth, long Health, double Inequality, long Economy, long Housing);

public sealed record CitySnapshot(
    CityId Id,
    string Name,
    CellCoord Location,
    AggregatePopulationPool AggregatePool,
    IReadOnlyList<CityResidentMarker> Residents,
    // Ids reservados (City.PoolNpcIds) de membros do pool agregado ainda não materializados
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
        var city = world.FindCity(cityId);
        if (city is null || city.MergedIntoCityId is not null)
            return Result<CitySnapshot>.Fail($"cidade {cityId} não encontrada");

        var residents = world.Npcs
            .Where(n => n.IsAlive && n.City == cityId)
            .Select(n => new CityResidentMarker(
                n.Id, n.CurrentLocation, n.CurrentAction, ExtraordinaryNpcVisualProjector.Build(world, n.Id)))
            .ToList();

        // Bounds resolvidos antes dos marcadores de prédio -- Resolve agora precisa deles pra
        // tentar uma célula livre dentro dos bounds antes de cair no overflow; ResolveGrownBounds
        // já alimenta os boxes de overflow de volta pra que os bounds realmente cresçam, não só
        // no teste unitário.
        long populationForBounds = CityPopulationQuery.Population(world, cityId);
        var (boundsForPlacement, boundsAreDerived) = CityOccupancy.ResolveGrownBounds(world, city, populationForBounds);

        // null = escassez de terra pra este prédio agora -- excluído dos
        // marcadores desta resposta em vez de derrubar a projeção inteira da cidade; nunca
        // persistido, então volta a aparecer assim que houver espaço de novo.
        // Preço de imóvel só se calcula quando o cenário declara Pricing/Lodging: caminho rápido sem custo para os demais.
        var recipes = world.CityCatalog.BuildingRecipes;
        bool pricesBuildings = recipes.Values.Any(r => r.Pricing is not null || r.Lodging is not null);
        var valuation = pricesBuildings ? new ValuationContext(world, city) : null;
        var tenants = pricesBuildings ? DwellingCapacity.TenantsByBuilding(world) : null;
        var buildings = world.Buildings
            .Where(b => b.City == cityId)
            .Select(b =>
            {
                if (BuildingPlacementResolver.Resolve(b, city, world, boundsForPlacement) is not { } resolved)
                    return null;
                var layout = BuildingLayoutGenerator.Generate(b.Id, b.BuildingTypeId, resolved.Orientation);
                var footprint = layout.FloorLayouts[0].Cells
                    .Select(cell => new CellCoord(
                        resolved.Position.X + cell.Cell.X,
                        resolved.Position.Y + cell.Cell.Y));
                // Overflow continua existindo no mundo e pode fundar/ser absorvido; apenas não é
                // desenhado como se estivesse dentro do grid desta cidade antes disso.
                if (!footprint.All(boundsForPlacement.Contains)) return null;
                long value = 0;
                LodgingMarker? lodging = null;
                if (valuation is not null && recipes.TryGetValue(b.BuildingTypeId, out var recipe))
                {
                    value = BuildingValuation.ValueOf(valuation, b, recipe);
                    if (recipe.Lodging is not null)
                        lodging = new LodgingMarker(
                            DwellingCapacity.Of(world, b), tenants!.GetValueOrDefault(b.Id),
                            BuildingValuation.RentOf(valuation, b, recipe), BuildingValuation.NightlyOf(valuation, b, recipe));
                }

                return new CityBuildingMarker(
                    b.Id, b.BuildingTypeId, resolved.Position, resolved.IsDerived, resolved.Orientation,
                    layout.Floors, layout.FloorLayouts[0].Cells, value, lodging);
            })
            .OfType<CityBuildingMarker>()
            .ToList();

        var layers = CityLayerBuilder.SupportedLayers.ToDictionary(id => id, CityLayerBuilder.Build);
        layers[VisualLayerId.Climate] = GlobalLayerBuilder.Build(VisualLayerId.Climate, world);

        // Portal cujo From ou To referencia esta cidade — mesma semântica de
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
        // resolvidos acima para os marcadores de prédio — nunca recalculado duas vezes.
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
