using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (Fase 8, T12): design.md não define a fórmula de pontuação por cidade — só que
// os 4 fatores (emprego, comida, segurança, laços familiares) são pesados por CityRules (R3).
// Cada fator normalizado em [0,1], score = soma dos pesos * nível:
//   - Emprego: fração de materializados da cidade com Npc.Employer != null (sem materializado,
//     nível neutro 1.0 — não há dado para julgar).
//   - Comida: mesmo nível 0-100 de CityGrowthSystem.FoodStock, normalizado pra [0,1].
//   - Segurança: nenhuma fonte de dado existe em Foundation — nível sempre neutro 0.5.
//   - Laços familiares: fração de MotherId/FatherId/Spouse vivos do household que já residem na
//     cidade candidata.
// Só household materializado decide (o Head precisa ter Npc real); migra pra cidade com maior
// score que a atual (empate mantém — sem margem extra inventada).

/// <summary>Household/NPC materializado decide migrar pesando emprego/comida/segurança/laços
/// familiares (Fase 8, T12, CITY-07) — inicia deslocamento para o destino; <see
/// cref="RelocationArrivalSystem"/> só muda <see cref="CityId"/> na chegada (Fase 15.1, T11,
/// LWV-04.2).</summary>
public sealed class MigrationSystem : ISimulationSystem
{
    public const string SystemName = "cities-migration";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    // dynamic-city-growth, T5 (Edge Case: mapa sem célula livre em lugar nenhum): footprint
    // representativo de "casa" só pra sondar escassez de terra via CityOccupancy.IsLandScarce —
    // não corresponde a nenhum prédio real, é só uma sonda de tamanho plausível.
    private static readonly IReadOnlyList<CellCoord> LandScarcityProbeShape =
        BuildingFootprintGenerator.Generate(new BuildingId(1), buildingTypeId: 1).Select(c => c.Cell).ToList();

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled || world.Cities.Count < 2) return;
        var rules = world.CityRules;
        // ponytail: cache por CityId dentro do Tick (não por household) — households da mesma
        // cidade não repetem o scan de mapa inteiro; cachear entre ticks só se aparecer no profiling.
        var scarcityCache = new Dictionary<CityId, bool>();
        bool IsLandScarce(CityId cityId)
        {
            if (scarcityCache.TryGetValue(cityId, out var cached)) return cached;
            var city = world.FindCity(cityId);
            bool scarce = city is not null && CityOccupancy.IsLandScarce(world, city, LandScarcityProbeShape);
            scarcityCache[cityId] = scarce;
            return scarce;
        }

        foreach (var household in world.Households.OrderBy(h => h.Id.Value).ToList())
        {
            var head = world.FindNpc(household.Head);
            if (head is not { IsAlive: true }) continue; // só household materializado decide

            var currentCity = head.City;
            City? bestCity = null;
            // T5/CITYGROW edge case: mapa sem célula livre em lugar nenhum força o score de
            // "ficar" pro mínimo teórico, sem afetar o score de nenhuma cidade candidata.
            double bestScore = IsLandScarce(currentCity) ? double.NegativeInfinity : ScoreOf(world, rules, household, currentCity);

            foreach (var candidate in world.Cities)
            {
                if (candidate.Id == currentCity) continue;
                double score = ScoreOf(world, rules, household, candidate.Id);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCity = candidate;
                }
            }

            if (bestCity is null) continue;

            household.BeginRelocation(bestCity.Id);
            foreach (var memberId in household.Members)
            {
                var member = world.FindNpc(memberId);
                if (member is not { IsAlive: true } || member.City != currentCity) continue;
                member.JoinHousehold(household.Id);
                member.SetCurrentAction(ActionType.Travel, ctx.CurrentTick);
            }
        }
    }

    private static double ScoreOf(WorldState world, CityRules rules, Household household, CityId cityId) =>
        rules.MigrationEmploymentWeight * EmploymentLevel(world, cityId)
        + rules.MigrationFoodWeight * FoodLevel(world, cityId)
        + rules.MigrationSecurityWeight * 0.5 // ver SPEC_DEVIATION acima
        + rules.MigrationFamilyTiesWeight * FamilyTiesLevel(world, household, cityId);

    private static double EmploymentLevel(WorldState world, CityId cityId)
    {
        var materialized = world.Npcs.Where(n => n.IsAlive && n.City == cityId).ToList();
        if (materialized.Count == 0) return 1.0;
        return materialized.Count(n => n.Employer is not null) / (double)materialized.Count;
    }

    private static double FoodLevel(WorldState world, CityId cityId)
    {
        long population = CityPopulationQuery.Population(world, cityId);
        if (population <= 0) return 1.0;
        var foodResource = new ResourceType(world.EconomyRules.FoodResourceId);
        long food = world.Households.Where(h => h.City == cityId).Sum(h => h.Stock.GetValueOrDefault(foodResource));
        return Math.Min(1.0, food / (double)population);
    }

    private static double FamilyTiesLevel(WorldState world, Household household, CityId cityId)
    {
        var ties = new List<NpcId>();
        foreach (var memberId in household.Members)
        {
            var member = world.FindNpc(memberId);
            if (member is null) continue;
            if (member.MotherId is { } m) ties.Add(m);
            if (member.FatherId is { } f) ties.Add(f);
            if (member.Spouse is { } s) ties.Add(s);
        }
        if (ties.Count == 0) return 0.0;

        int inCity = ties.Count(id => world.FindNpc(id) is { IsAlive: true } relative && relative.City == cityId);
        return inCity / (double)ties.Count;
    }
}
