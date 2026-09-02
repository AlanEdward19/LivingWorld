using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Founding;

/// <summary>Checa mensalmente se algum cluster de prédios de overflow de uma cidade (dynamic-city-growth,
/// T7, CITYGROW-04) já reúne população materializada suficiente pra fundar uma cidade nova — a
/// MESMA fórmula/limiar que <see cref="SettlementFoundingSystem"/> já usa pra fundação normal
/// (<c>população / (população + 1) &gt;= CityRules.FoundingConcentrationThreshold</c>), nunca um
/// limiar mais fraco por contagem de prédios (spec: "1 casa não funda uma cidade, 1 pessoa não
/// funda uma cidade, uma sociedade funda"). Ao bater o limiar, agenda um evento único em
/// <c>now + CityRules.OrganizationTicks</c> (mesmo padrão de <see cref="SettlementFoundingSystem"/>),
/// guardado pelo marcador <see cref="Building.ClusterFoundingScheduledAtTick"/> em cada prédio do
/// cluster capturado. Ao disparar, reverifica o limiar (o cluster pode ter esvaziado durante a
/// espera) e, se ainda válido, funda a <see cref="City"/> no centroide do cluster, reatribuindo
/// prédios e households/Npcs geometricamente dentro dos bounds iniciais da cidade nova.</summary>
public sealed class SpatialSettlementFoundingSystem : ISimulationSystem
{
    public const string SystemName = "cities-spatial-settlement-founding";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Monthly;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;
        var rules = world.CityRules;

        ScheduleAdjacentDaughterMerges(world, ctx, rules);

        foreach (var city in world.ActiveCities().OrderBy(city => city.Id.Value))
        {
            foreach (var cluster in OverflowClusterFinder.FindClusters(world, city))
            {
                if (cluster.Buildings.Any(b => b.ClusterFoundingScheduledAtTick is not null))
                    continue; // já agendado (mesmo cluster ou um que o sobrepõe), não reagenda
                if (!ClearsConcentrationThreshold(cluster.Population, rules))
                    continue; // prédios sem (ou com poucos) residentes reais nunca fundam nada

                string payload = $"{city.Id.Value}|{string.Join(",", cluster.Buildings.Select(b => b.Id.Value))}";
                ctx.ScheduleEvent(ctx.CurrentTick + rules.OrganizationTicks, SystemName, payload);
                foreach (var building in cluster.Buildings)
                    building.MarkClusterFoundingScheduled(ctx.CurrentTick);
            }
        }
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        if (evt.Payload!.StartsWith("merge|", StringComparison.Ordinal))
        {
            HandleMergeEvent(world, ctx, evt.Payload);
            return;
        }

        var parts = evt.Payload!.Split('|');
        var motherCityId = new CityId(Guid.Parse(parts[0]));
        var capturedBuildingIds = parts[1].Split(',').Select(long.Parse).ToHashSet();

        var motherCity = world.FindCity(motherCityId);
        if (motherCity is null || motherCity.MergedIntoCityId is not null) return;

        var buildings = world.Buildings
            .Where(b => capturedBuildingIds.Contains(b.Id.Value) && b.City == motherCityId)
            .ToList();
        if (buildings.Count == 0) return; // cluster inteiro já reatribuído/removido nesse meio tempo

        long motherPopulation = CityPopulationQuery.Population(world, motherCityId);
        var (motherPopulationBounds, _) =
            SpatialBoundsResolver.ResolveCity(motherCity, motherPopulation, world.Map.Width, world.Map.Height);
        var ownedBoxes = CityOccupancy.OwnedBuildingFootprintBoxesWithOwners(world, motherCity, motherPopulationBounds)
            .Where(p => capturedBuildingIds.Contains(p.Building.Id.Value))
            .Select(p => p.Box)
            .ToList();
        if (ownedBoxes.Count == 0) return;

        var clusterBounds = OverflowClusterFinder.UnionBounds(ownedBoxes);
        long population = world.Npcs.Count(npc => npc.IsAlive && clusterBounds.Contains(npc.CurrentLocation));
        if (!ClearsConcentrationThreshold(population, world.CityRules))
            return; // cluster esvaziou durante a espera — dropado silenciosamente, nunca força uma cidade injustificada

        // Post-ship fix (Fix 2, 2026-08-23): a distância de absorção só era checada no AGENDAMENTO
        // (Tick, via OverflowClusterFinder), nunca reverificada aqui no disparo -- se outra cidade
        // cresceu (absorveu mais overflow) durante a espera de OrganizationTicks e passou a ficar
        // dentro do alcance de absorção deste cluster, fundar mesmo assim produziria uma cidade
        // nova colada na vizinha (o próprio bug relatado). Mesmo padrão da reverificação de
        // concentração acima: dropa silenciosamente em vez de forçar uma fundação injustificada.
        if (OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity(world, motherCityId, clusterBounds, world.CityRules.AbsorptionRingCells))
            return; // absorção por uma cidade vizinha tem precedência sobre fundar uma nova (spec Edge Cases)

        var centroid = new CellCoord(
            clusterBounds.Origin.X + clusterBounds.Width / 2,
            clusterBounds.Origin.Y + clusterBounds.Height / 2);

        // Post-ship fix (user-reported, 2026-08-23, "MorNorHol" founded off-map): unlike
        // FoundingSitePicker.Pick (which validates every candidate against world.Map.Width/Height
        // before returning it), this centroid came straight from OverflowClusterFinder.UnionBounds
        // with no map-bounds check at all -- an authored building placed off-map (separate,
        // pre-existing gap, not fixed here) could feed an off-map cluster into a real founding.
        // Same silent-drop convention as the two checks above: an overflow cluster that only
        // exists because of an off-map building is a symptom of THAT gap, not something to paper
        // over by clamping a nonsensical centroid into a random on-map cell.
        if (centroid.X < 0 || centroid.Y < 0 || centroid.X >= world.Map.Width || centroid.Y >= world.Map.Height)
            return; // centroide fora do mapa -- dropado silenciosamente, nunca força uma cidade fora do mundo

        var newCity = new City(
            world.NextCityId(), centroid, ctx.CurrentTick, motherCityId, AggregatePopulationPool.Empty,
            name: CityNameGenerator.Generate(world));
        world.AddCity(newCity);

        foreach (var building in buildings)
            building.JoinCity(newCity.Id);

        // Locais de trabalho fisicamente contidos no cluster passam a pertencer ao novo
        // assentamento junto com seus prédios. Sem isto, fundadores continuavam empregados na
        // cidade-mãe e atravessavam o mapa de volta no próximo turno de trabalho.
        foreach (var workplace in world.Workplaces
                     .Where(workplace => (workplace.City == motherCityId || workplace.City == default)
                                           && clusterBounds.Contains(workplace.Location))
                     .OrderBy(workplace => workplace.Id.Value))
            workplace.JoinCity(newCity.Id);

        // dynamic-city-growth, T7: "bounds iniciais da cidade nova" = a própria caixa do cluster
        // (não um box só-população, que pode ficar menor que o cluster que a fundou e nem conter
        // os prédios/households que a fundaram) — mesmo espírito de bounds.Contains(location) de
        // NpcScopeResolver, aplicado aqui à geometria real que já existe.
        //
        // Household.Location representa a residência estável anterior; para descobrir quem
        // realmente formou um cluster novo, usamos a posição corrente do chefe no instante da
        // fundação. Depois da reatribuição, essa posição passa a ser a nova residência estável.
        // Post-ship fix (round 2, 2026-08-23, "population jumping between two adjacent cities"):
        // this loop swept up ANY household in the world whose head currently stands inside
        // clusterBounds, with no check that the household actually belonged to the founding
        // cluster's own mother city -- a household already settled in a NEIGHBORING city (which
        // is expected to be geometrically close, since it took Fix 1's cross-city bounds clamp to
        // even let two cities coexist this near each other) got poached back and forth every
        // monthly re-scan just because its head happened to be standing in this cluster's
        // footprint at this exact tick. Only households that were genuinely part of motherCityId
        // (the cluster's real origin) may be reassigned.
        foreach (var household in world.Households.OrderBy(household => household.Id.Value))
        {
            if (household.City != motherCityId) continue;
            if (world.FindNpc(household.Head) is not { IsAlive: true } head) continue;
            if (!clusterBounds.Contains(head.CurrentLocation)) continue;

            // CurrentLocation identifica quem fundou o cluster; a partir daqui ela se torna a
            // residência estável usada por sono e pelas próximas avaliações de migração.
            household.JoinCity(newCity.Id, head.CurrentLocation);
            foreach (var memberId in household.Members.OrderBy(id => id.Value))
            {
                var member = world.FindNpc(memberId);
                if (member is null) continue;
                member.JoinCity(newCity.Id);
            }
        }
    }

    private static bool ClearsConcentrationThreshold(long population, CityRules rules) =>
        population / (population + 1.0) >= rules.FoundingConcentrationThreshold;

    private static void ScheduleAdjacentDaughterMerges(WorldState world, TickContext ctx, CityRules rules)
    {
        foreach (var daughter in world.ActiveCities()
                     .Where(city => city.FoundedFromCityId is not null)
                     .OrderBy(city => city.Id.Value))
        {
            if (daughter.MergeScheduledAtTick is not null) continue;
            var mother = world.FindActiveCity(daughter.FoundedFromCityId!.Value);
            if (mother is null || mother.Id == daughter.Id || !AreAdjacent(world, mother, daughter, rules.AbsorptionRingCells))
                continue;

            ctx.ScheduleEvent(
                ctx.CurrentTick + rules.OrganizationTicks,
                SystemName,
                $"merge|{daughter.Id.Value}|{mother.Id.Value}");
            daughter.MarkMergeScheduled(ctx.CurrentTick);
        }
    }

    private static void HandleMergeEvent(WorldState world, TickContext ctx, string payload)
    {
        var parts = payload.Split('|');
        var daughter = world.FindCity(new CityId(Guid.Parse(parts[1])));
        if (daughter is null || daughter.MergedIntoCityId is not null) return;

        var mother = world.FindActiveCity(new CityId(Guid.Parse(parts[2])));
        if (mother is null || !AreAdjacent(world, mother, daughter, world.CityRules.AbsorptionRingCells))
        {
            daughter.ClearMergeScheduled();
            return;
        }

        world.MergeCityInto(daughter, mother);
        ctx.LogEvent(WorldEventKind.CityMerged, $"{daughter.Id.Value}|{mother.Id.Value}", sourceSystem: "SpatialSettlementFoundingSystem");
    }

    private static bool AreAdjacent(WorldState world, City mother, City daughter, int ring)
    {
        var motherBounds = CityOccupancy.ResolveGrownBounds(
            world, mother, CityPopulationQuery.Population(world, mother.Id)).Bounds;
        var daughterBounds = CityOccupancy.ResolveGrownBounds(
            world, daughter, CityPopulationQuery.Population(world, daughter.Id)).Bounds;
        return OverflowClusterFinder.ChebyshevGap(motherBounds, daughterBounds) <= ring;
    }
}
