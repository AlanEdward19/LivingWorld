using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (dynamic-city-growth, T3): movido de LivingWorld.Domain para
// LivingWorld.Simulation -- design.md mantém este arquivo em src/LivingWorld.Domain/Cities/, mas
// a nova assinatura precisa de WorldState (para consultar CityOccupancy/OverflowPlacer) e
// WorldState só existe em Simulation (Domain não referencia Simulation, ver os .csproj). Mesmo
// motivo/precedente de CityOccupancy.cs/OverflowPlacer.cs (T1/T2) e de CityPopulationQuery.cs.
// Todo call site já importava LivingWorld.Simulation (verificado por grep antes desta mudança),
// então a migração de namespace não exige nenhum using novo.

/// <summary>Resolve posição/orientação de um prédio (Fase 15.1, T45; G4/backend-gaps.md;
/// dynamic-city-growth T3): autoria (T44 — <see cref="Building.Position"/> não nulo) tem
/// precedência. Sem autoria, tenta primeiro uma célula livre dentro dos bounds atuais da cidade
/// (<see cref="CityOccupancy.FindFreeCellInBounds"/> — nunca mais sobrepõe silenciosamente outro
/// prédio, CITYGROW-01) e só cai no anel de overflow (<see
/// cref="OverflowPlacer.ResolveOverflowPosition"/>, CITYGROW-02) quando os bounds estão
/// totalmente ocupados.
///
/// dynamic-city-growth, fix (major, CITYGROW-02b): <c>null</c> quando nem os bounds nem o anel de
/// overflow acham uma célula livre em lugar nenhum do mapa -- escassez de terra real. Chamadores
/// tratam isso deixando o prédio sem posição resolvida por esta chamada (sem fila, sem retry
/// especial -- tenta de novo na próxima vez que alguém pedir, mesmo espírito de "sempre re-derivado,
/// nunca persistido" que o resto deste tipo já segue), nunca um crash.</summary>
public static class BuildingPlacementResolver
{
    public static (CellCoord Position, int Orientation, bool IsDerived)? Resolve(
        Building building, City city, WorldState world, CityBounds bounds)
    {
        if (building.Position is { } position)
            return (position, building.Orientation ?? 0, false);

        var shape = BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId).Select(c => c.Cell).ToList();
        var origin = CityOccupancy.FindFreeCellInBounds(world, city, bounds, shape, building.Id)
            ?? OverflowPlacer.ResolveOverflowPosition(world, city, bounds, building.Id, shape);

        return origin is { } resolved ? (resolved, 0, true) : null;
    }

    /// <summary>Canteiro de obra ainda sem <see cref="BuildingId"/> — estável por índice da fila
    /// (T19 / LWV-04.4). Não é o cell final do prédio concluído; nunca passou pela ocupação
    /// real, só o anel fixo de sempre (T45), por isso não precisa de <see cref="WorldState"/>.</summary>
    public static CellCoord ResolveQueuedSite(City city, int queueIndex) =>
        CityOccupancy.LegacyRingFallback(new BuildingId(-(queueIndex + 1)), city.Location);
}
