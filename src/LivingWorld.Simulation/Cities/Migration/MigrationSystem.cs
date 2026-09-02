using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Migration;

// SPEC_DEVIATION (Fase 8, T12): design.md não define a fórmula de pontuação por cidade — só que
// os 4 fatores (emprego, comida, segurança, laços familiares) são pesados por CityRules (R3).
// Cada fator normalizado em [0,1], score = soma dos pesos * nível:
//   - Emprego: fração de materializados da cidade com Npc.Employer != null (sem materializado,
//     nível neutro 1.0 — não há dado para julgar).
//   - Comida: mesmo nível 0-100 de CityGrowthSystem.FoodStock, normalizado pra [0,1].
//   - Segurança: nenhuma fonte de dado existe em Foundation — nível sempre neutro 0.5.
//   - Laços familiares: fração de MotherId/FatherId/Spouse vivos do household que já residem na
//     cidade candidata.
// Só household materializado e sob uma necessidade real decide (falta de emprego/comida,
// residência fora dos bounds ou escassez total de terra). O score escolhe o destino; ele não é,
// sozinho, motivo para abandonar uma vida estável.

/// <summary>Household/NPC materializado decide migrar pesando emprego/comida/segurança/laços
/// familiares (Fase 8, T12, CITY-07) — inicia deslocamento para o destino; <see
/// cref="RelocationArrivalSystem"/> só muda <see cref="CityId"/> na chegada (Fase 15.1, T11,
/// LWV-04.2).</summary>
public sealed class MigrationSystem : ISimulationSystem
{
    public const string SystemName = "cities-migration";

    // dynamic-city-growth post-ship fix (user-reported, 2026-08-23): two cities close enough to
    // legitimately coexist (now supported after the CityBoundsResolver/MigrationSystem-adjacent
    // fixes that day) made households flip-flop daily -- moving a household shifts the very
    // population/food counts (EmploymentLevel/FoodLevel, both recomputed live) that feed
    // tomorrow's score for the OTHER city, so a strict `score > bestScore` self-reinforcingly
    // ping-pongs. ScoreOf's scale is weight-dependent (CityRules configures each weight freely,
    // not normalized to sum to 1), so a relative margin -- not a fixed additive one -- is the
    // only choice that stays proportionate to whatever weights a scenario configures. 15% chosen
    // as a "meaningful, not marginal" improvement threshold, matching the real-world intuition a
    // household relocates for a substantial gain, not a rounding-error one; no cooldown/timer
    // added since a plain margin is enough to stop the oscillation this file's own tests reproduce
    // (see MigrationSystemTests) and every other rule in this file is already timer-free.
    private const double HysteresisMargin = 0.15;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    // dynamic-city-growth, T5 (Edge Case: mapa sem célula livre em lugar nenhum): footprint
    // representativo de "casa" só pra sondar escassez de terra via CityOccupancy.IsLandScarce —
    // não corresponde a nenhum prédio real, é só uma sonda de tamanho plausível.
    private static readonly IReadOnlyList<CellCoord> LandScarcityProbeShape =
        BuildingFootprintGenerator.Generate(new BuildingId(1), buildingTypeId: 1).Select(c => c.Cell).ToList();

    public void Tick(WorldState world, TickContext ctx)
    {
        var activeCities = world.ActiveCities().OrderBy(city => city.Id.Value).ToList();
        if (!world.CityRules.Enabled || activeCities.Count < 2) return;
        var rules = world.CityRules;
        // ponytail: cache por CityId dentro do Tick (não por household) — households da mesma
        // cidade não repetem o scan de mapa inteiro; cachear entre ticks só se aparecer no profiling.
        var scarcityCache = new Dictionary<CityId, bool>();
        bool IsLandScarce(CityId cityId)
        {
            if (scarcityCache.TryGetValue(cityId, out var cached)) return cached;
            var city = world.FindActiveCity(cityId);
            bool scarce = city is not null && CityOccupancy.IsLandScarce(world, city, LandScarcityProbeShape);
            scarcityCache[cityId] = scarce;
            return scarce;
        }

        foreach (var household in world.Households.OrderBy(h => h.Id.Value).ToList())
        {
            if (household.PendingRelocationCity is not null) continue;
            var head = world.FindNpc(household.Head);
            if (head is not { IsAlive: true }) continue; // só household materializado decide

            var currentCity = head.City;
            bool landScarce = IsLandScarce(currentCity);
            if (!NeedsMigration(world, rules, household, head, landScarce, activeCities)) continue;

            City? bestCity = null;
            // T5/CITYGROW edge case: mapa sem célula livre em lugar nenhum força o score de
            // "ficar" pro mínimo teórico, sem afetar o score de nenhuma cidade candidata.
            double bestScore = landScarce ? double.NegativeInfinity : ScoreOf(world, rules, household, currentCity);

            foreach (var candidate in activeCities)
            {
                if (candidate.Id == currentCity) continue;
                double score = ScoreOf(world, rules, household, candidate.Id);
                // Hysteresis: a candidate must beat bestScore by more than HysteresisMargin
                // (relative) to be worth relocating for -- see HysteresisMargin doc above. When
                // bestScore is double.NegativeInfinity (land-scarce "ficar" case above), the
                // margin stays NegativeInfinity too, so any finite score still wins unconditionally
                // (land scarcity must always force relocation, never gated by the margin).
                if (score > bestScore * (1 + HysteresisMargin))
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

    private static bool NeedsMigration(
        WorldState world, CityRules rules, Household household, Npc head, bool landScarce,
        IReadOnlyList<City> activeCities)
    {
        if (landScarce)
            return true;

        long aliveMembers = household.Members.Count(id => world.FindNpc(id) is { IsAlive: true });
        var foodResource = new ResourceType(world.EconomyRules.FoodResourceId);
        double minimumFood = aliveMembers * (1.0 - rules.FoodShortageThreshold / 100.0);
        bool householdLacksFood = household.Stock.GetValueOrDefault(foodResource) < minimumFood;
        bool cityLacksFood = FoodLevel(world, head.City) < 1.0 - rules.FoodShortageThreshold / 100.0;
        if (householdLacksFood && cityLacksFood)
            return true;

        if (world.EconomyRules.Enabled && head.Employer is null
            && activeCities.Any(city => city.Id != head.City && HasVacancyFor(world, head, city.Id)))
            return true;

        var city = world.FindActiveCity(head.City);
        if (city is null)
            return true;

        long population = CityPopulationQuery.Population(world, city.Id);
        var (bounds, _) = CityOccupancy.ResolveGrownBounds(world, city, population);
        // A posição corrente pode estar fora da cidade por trabalho, visita ou deslocamento.
        // Só a residência estável fora dos bounds representa falta real de moradia local.
        return !bounds.Contains(household.Location);
    }

    private static bool HasVacancyFor(WorldState world, Npc npc, CityId cityId)
    {
        if (!world.EconomyCatalog.LocationTypeByProfession.TryGetValue(npc.Profession.Id, out int locationTypeId))
            return false;

        return world.Workplaces.Any(workplace =>
            workplace.City == cityId
            && workplace.LocationType.Id == locationTypeId
            && workplace.Employees.Count < workplace.MaxVacancies);
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
