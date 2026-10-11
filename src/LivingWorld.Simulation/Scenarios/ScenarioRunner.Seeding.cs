using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Geography.Map;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Simulation.Scenarios;

// Parte de ScenarioRunner — mapa inicial e semeadura de locais de trabalho e estoque. Os padrões estáticos ficam em ScenarioRunner.cs (ordem de inicialização).
public static partial class ScenarioRunner
{
    public static WorldMap DefaultMap(ulong seed) =>
        MapGenerator.Generate(seed, width: 10, height: 10, regionSize: 5, DefaultCatalog, DefaultCostWeights, [])
            .Value ?? throw new InvalidOperationException("gerador default falhou — bug no gerador, não no cenário");

    internal static int InitialMapSideForPopulation(int initialPopulation)
    {
        const int regionSize = 5;
        const int maxBuildingFootprintCells = 6 * 5;
        long buildingSlots = Math.Max(0, (long)initialPopulation) + 2;
        int requiredSide = (int)Math.Ceiling(Math.Sqrt(buildingSlots * maxBuildingFootprintCells));
        int roundedSide = ((requiredSide + regionSize - 1) / regionSize) * regionSize;
        return Math.Max(10, roundedSide);
    }

    internal static WorldMap InitialMap(ulong seed, int initialPopulation)
    {
        int side = InitialMapSideForPopulation(initialPopulation);
        return MapGenerator.Generate(seed, side, side, regionSize: 5, DefaultCatalog, DefaultCostWeights, [])
            .Value ?? throw new InvalidOperationException("gerador de mapa inicial falhou — bug no gerador, não no cenário");
    }

    /// <summary>Fazenda e ferraria iniciais, sem empregado (contratados pelo <see
    /// cref="EmploymentSystem"/> no primeiro Daily), sem estoque (produzido pelo <see
    /// cref="ProductionSystem"/>). Preço inicial 5 (não 1, o piso): <see
    /// cref="MarketPricingSystem"/> é multiplicativo — arredondado pra inteiro, preço 1 nunca sai
    /// do lugar de verdade (fator 0.8 ou 1.2 sobre 1 arredonda de volta pra 1), escondendo
    /// qualquer sinal de escassez/fartura (achado escrevendo o teste causal de T25).</summary>
    private static void SeedDefaultWorkplaces(WorldState world, City city, int vacancyMultiplier = 1)
    {
        int mult = Math.Max(1, vacancyMultiplier);
        // Treasury inicial grande (capital de giro do dono, estado inicial declarado — não
        // cunhagem): folha de ~36 empregados a 90-110/mês esgotaria
        // um treasury pequeno bem antes da receita de venda (compras em lote esporádicas)
        // acompanhar, gerando WageUnpaid em cascata e, por tabela, fome real generalizada.
        var farmLocation = PlaceDefaultWorkplaceBuilding(world, city, buildingTypeId: 1);
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(1), farmLocation, maxVacancies: 80 * mult,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: new Money(500_000),
            prices: new Dictionary<ResourceType, long> { [new ResourceType(1)] = 5, [new ResourceType(2)] = 5 },
            city: city.Id));
        var forgeLocation = PlaceDefaultWorkplaceBuilding(world, city, buildingTypeId: 2);
        world.AddWorkplace(new Workplace(
            world.NextWorkplaceIdAndAdvance(), new LocationType(2), forgeLocation, maxVacancies: 40 * mult,
            employees: [], stock: new Dictionary<ResourceType, long>(), treasury: new Money(500_000),
            prices: new Dictionary<ResourceType, long> { [new ResourceType(4)] = 5 }, city: city.Id));
    }

    private static CellCoord PlaceDefaultWorkplaceBuilding(WorldState world, City city, int buildingTypeId)
    {
        var candidate = new Building(
            new BuildingId(world.NextBuildingId), city.Id, buildingTypeId, world.CurrentDate.TotalHours);
        long population = CityPopulationQuery.Population(world, city.Id);
        var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
        var resolved = BuildingPlacementResolver.Resolve(candidate, city, world, bounds)
            ?? throw new InvalidOperationException("No free cell is available for a default workplace building.");

        world.AddBuilding(new Building(
            world.NextBuildingIdAndAdvance(), city.Id, buildingTypeId, world.CurrentDate.TotalHours,
            resolved.Position, resolved.Orientation));
        return resolved.Position;
    }

    /// <summary>Trigo/água de despensa + moeda no bolso pra cada NPC/Household inicial —
    /// ponte pro primeiro salário (<see cref="WagePaymentSystem"/> é Monthly): sem isso, todo
    /// mundo bate fome/sede zero e morre de fome (NEEDS-03) bem antes do primeiro emprego
    /// (<see cref="EmploymentSystem"/>, Daily) gerar produção e renda de verdade — o cenário
    /// default se extinguiria em semanas, não sobreviveria os 100 anos do objetivo #1.
    /// Quantidade cobre só o mês de bootstrap; da­í em diante o ciclo emprego→produção→salário→
    /// compra é quem sustenta.</summary>
    private static void SeedInitialEconomyBuffer(WorldState world)
    {
        foreach (var household in world.Households)
        {
            long bootstrapUnits = 50L * household.Members.Count;
            household.Deposit(new ResourceType(1), bootstrapUnits); // trigo
            household.Deposit(new ResourceType(2), bootstrapUnits); // água
        }

        foreach (var npc in world.Npcs)
            npc.CreditWallet(new Money(50));
    }
}
