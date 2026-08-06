using LivingWorld.Api.Visual.Layers;
using LivingWorld.Domain;
using LivingWorld.Simulation;

namespace LivingWorld.Api.Visual;

/// <summary>Fase 15, T4 (VTT-01, VTT-04, VTT-06): marcador de cidade no mapa-múndi — só posição
/// e população agregada (<see cref="CityPopulationQuery"/>), sem detalhe de residente.</summary>
public sealed record GlobalCityMarker(CityId Id, CellCoord Location, long Population);

/// <summary>Fase 15, T4 (VTT-01, VTT-06): NPC materializado fora da célula da própria cidade —
/// "externo" no sentido do espectador global (spec.md: "NPCs externos agregados por LOD"),
/// mostrado como marcador simplificado (só posição), nunca com o detalhe de <c>NpcInspectionDto</c>.</summary>
public sealed record GlobalNpcMarker(NpcId Id, CellCoord Location);

/// <summary>Fase 15, T4: projeção do mapa-múndi simplificado. <c>ActiveEvents</c> fica sempre
/// vazio por ora — o motor não tem noção de "evento em andamento" (só histórico ponto-a-ponto em
/// <c>Facts</c>/event log), então resumir isso aqui seria inventar semântica sem lastro; fica
/// deferido até essa leitura existir no domínio.</summary>
public sealed record GlobalSnapshot(
    IReadOnlyList<GlobalCityMarker> Cities,
    IReadOnlyList<GlobalNpcMarker> ExternalNpcs,
    IReadOnlyList<object> ActiveEvents,
    IReadOnlyDictionary<VisualLayerId, LayerBuildResult> Layers);

public static class GlobalProjector
{
    public static GlobalSnapshot Build(WorldState world)
    {
        var cities = world.Cities
            .Select(c => new GlobalCityMarker(c.Id, c.Location, CityPopulationQuery.Population(world, c.Id)))
            .ToList();

        // NPCs cuja CityId ainda não tem um City real em world.Cities (cidade não fundada/
        // seedada ainda) não têm "casa" conhecida — não dá pra julgar "fora do lugar" sem uma
        // referência, então ficam de fora do marcador (não é um bug do NPC, é ausência de dado).
        var cityLocationById = world.Cities.ToDictionary(c => c.Id, c => c.Location);
        var externalNpcs = world.Npcs
            .Where(n => n.IsAlive && cityLocationById.TryGetValue(n.City, out var home) && n.CurrentLocation != home)
            .Select(n => new GlobalNpcMarker(n.Id, n.CurrentLocation))
            .ToList();

        var layers = GlobalLayerBuilder.SupportedLayers
            .ToDictionary(id => id, id => GlobalLayerBuilder.Build(id, world));

        return new GlobalSnapshot(cities, externalNpcs, [], layers);
    }
}
