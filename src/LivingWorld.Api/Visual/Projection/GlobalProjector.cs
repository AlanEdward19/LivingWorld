using LivingWorld.Api.Visual.Layers;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Spatial;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Api.Visual.Projection;

/// <summary>Forma achatada de <see cref="CityBounds"/> pro shape que o
/// cliente já espera (<c>web/src/data/contracts.ts</c> <c>CellBounds</c>) — <see
/// cref="CityBounds.Origin"/> nunca serializaria como <c>x</c>/<c>y</c> soltos por padrão.</summary>
public sealed record CellBounds(int X, int Y, int Width, int Height);

/// <summary>Marcador de cidade no mapa-múndi — só posição
/// e população agregada (<see cref="CityPopulationQuery"/>), sem detalhe de residente. <see
/// cref="Bounds"/>/<see cref="BoundsAreDerived"/> vêm de <see
/// cref="SpatialBoundsResolver.ResolveCity"/> — projeção derivada, não toca o domínio.</summary>
public sealed record GlobalCityMarker(
    CityId Id, string Name, CellCoord Location, long Population, CellBounds Bounds,
    bool BoundsAreDerived, long KnownCarrierCount = 0);

/// <summary>NPC materializado fora da célula da própria cidade —
/// "externo" no sentido do espectador global (spec.md: "NPCs externos agregados por LOD"),
/// mostrado como marcador simplificado (só posição), nunca com o detalhe de <c>NpcInspectionDto</c>.</summary>
public sealed record GlobalNpcMarker(
    NpcId Id, CellCoord Location, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Domain.Population.Appearance? Appearance = null);

/// <summary>Projeção do mapa-múndi simplificado. <c>ActiveEvents</c> fica sempre
/// vazio por ora — o motor não tem noção de "evento em andamento" (só histórico ponto-a-ponto em
/// <c>Facts</c>/event log), então resumir isso aqui seria inventar semântica sem lastro; fica
/// deferido até essa leitura existir no domínio.</summary>
public sealed record GlobalSnapshot(
    int Width,
    int Height,
    IReadOnlyList<GlobalCityMarker> Cities,
    IReadOnlyList<GlobalNpcMarker> ExternalNpcs,
    IReadOnlyList<object> ActiveEvents,
    IReadOnlyDictionary<VisualLayerId, LayerBuildResult> Layers,
    IReadOnlyList<SpatialPortal> Portals,
    LivingScopeState LivingState);

public static class GlobalProjector
{
    public static GlobalSnapshot Build(WorldState world)
    {
        var cities = world.ActiveCities()
            .Select(c =>
            {
                long population = CityPopulationQuery.Population(world, c.Id);
                // ResolveGrownBounds alimenta os boxes de overflow das próprias buildings da
                // cidade de volta pra Resolve, fazendo os bounds crescerem de verdade no
                // marcador do mapa-múndi.
                var (bounds, isDerived) = CityOccupancy.ResolveGrownBounds(world, c, population);
                var cellBounds = new CellBounds(bounds.Origin.X, bounds.Origin.Y, bounds.Width, bounds.Height);
                long knownCarrierCount = world.ExtraordinaryCarriers.LongCount(carrier =>
                    CityOwnsCarrier(world, c, carrier.CarrierId));
                return new GlobalCityMarker(
                    c.Id, c.Name, c.Location, population, cellBounds, isDerived, knownCarrierCount);
            })
            .ToList();

        // NPCs cuja CityId ainda não tem um City real em world.Cities (cidade não fundada/
        // seedada ainda) não têm "casa" conhecida — não dá pra julgar "fora do lugar" sem uma
        // referência, então ficam de fora do marcador (não é um bug do NPC, é ausência de dado).
        var cityBoundsById = world.ActiveCities().ToDictionary(
            c => c.Id,
            c => CityOccupancy.ResolveGrownBounds(world, c, CityPopulationQuery.Population(world, c.Id)).Bounds);
        // Mesmo critério geométrico de NpcScopeResolver (Domain), agora compartilhado com
        // LivingScopeProjector.IsNpcInScope e NpcInspectionQuery.ResolveScope.
        var externalNpcs = world.Npcs
            .Where(n => n.IsAlive && cityBoundsById.TryGetValue(n.City, out var home)
                && NpcScopeResolver.Resolve(n, home).Kind == NpcScopeKind.World)
            .Select(n => new GlobalNpcMarker(n.Id, n.CurrentLocation, n.Appearance))
            .ToList();

        var layers = GlobalLayerBuilder.SupportedLayers
            .ToDictionary(id => id, id => GlobalLayerBuilder.Build(id, world));

        // Portal cujo From ou To toca o escopo World — mesma semântica de
        // MockPortalSource.portalsOf (web/src/data/mock/MockPortalSource.ts), para trocar a
        // fonte no futuro sem mudar comportamento.
        var portals = world.Portals
            .Where(p => p.From.Space == PortalSpaceKind.World || p.To.Space == PortalSpaceKind.World)
            .ToList();

        var livingState = LivingScopeProjector.Build(world, new VisualScope(VisualScopeKind.World, ""));
        return new GlobalSnapshot(world.Map.Width, world.Map.Height, cities, externalNpcs, [], layers, portals, livingState);
    }

    private static bool CityOwnsCarrier(WorldState world, City city, NpcId carrierId) =>
        world.FindNpc(carrierId) is { IsAlive: true } npc
            ? npc.City == city.Id
            : city.PoolNpcIds.Contains(carrierId);
}
