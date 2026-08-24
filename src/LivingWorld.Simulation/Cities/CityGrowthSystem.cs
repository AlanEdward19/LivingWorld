using LivingWorld.Domain;

namespace LivingWorld.Simulation;

// SPEC_DEVIATION (Fase 8, T11): design.md/tasks.md não definem a fórmula exata de
// "comida/moradia/segurança abaixo do limiar" — só que o limiar/taxa vêm de CityRules (R3).
// Interpretação escolhida, ancorada em dado já existente (nenhum literal novo além da convenção
// de normalização 0-100, que não é limiar — os limiares continuam 100% CityRules):
//   - Moradia: soma de HousingCapacityProvided dos Building concluídos da cidade, como % da
//     população (100% = 1 vaga por residente).
//   - Comida: soma do estoque do recurso EconomyRules.FoodResourceId nos Household da cidade,
//     como % da população (100% = 1 unidade por residente).
//   - Segurança: nenhuma fonte de dado existe em Foundation (nenhum sistema de guarda/crime) —
//     nível sempre 100 (sem déficit). SecurityShortageThreshold nunca é ultrapassado nesta fase;
//     fica pronto para quando um sinal real existir.
// O déficit usado é o pior dos três fatores (o gargalo que mais empurra gente pra fora), nunca a
// soma (evitaria contar a mesma pressão duas vezes).

/// <summary>Emigração agregada do pool de uma cidade quando comida/moradia/segurança caem abaixo
/// do limiar do cenário (Fase 8, T11, CITY-02) — nunca tira NPC materializado (isso é
/// <c>MigrationSystem</c>, T12).</summary>
public sealed class CityGrowthSystem : ISimulationSystem
{
    public const string SystemName = "cities-growth";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;
        var rules = world.CityRules;

        foreach (var city in world.ActiveCities())
        {
            long population = CityPopulationQuery.Population(world, city.Id);
            if (population <= 0 || city.AggregatePool.Count <= 0) continue;

            double housingDeficit = 100.0 - LevelPercent(CityPopulationQuery.Housing(world, city.Id), population);
            double foodDeficit = 100.0 - LevelPercent(FoodStock(world, city.Id), population);
            const double securityDeficit = 0.0; // ver SPEC_DEVIATION acima

            double worstExcess = Math.Max(
                Math.Max(Math.Max(0, housingDeficit - rules.HousingShortageThreshold), Math.Max(0, foodDeficit - rules.FoodShortageThreshold)),
                Math.Max(0, securityDeficit - rules.SecurityShortageThreshold));

            if (worstExcess <= 0) continue;

            long emigrants = (long)Math.Floor(rules.EmigrationRatePerDeficitUnit * worstExcess);
            emigrants = Math.Clamp(emigrants, 0, city.AggregatePool.Count);
            if (emigrants <= 0) continue;

            city.Emigrate(emigrants);
        }
    }

    private static double LevelPercent(long available, long population) =>
        Math.Min(100.0, available * 100.0 / population);

    private static long FoodStock(WorldState world, CityId cityId)
    {
        var foodResource = new ResourceType(world.EconomyRules.FoodResourceId);
        return world.Households.Where(h => h.City == cityId).Sum(h => h.Stock.GetValueOrDefault(foodResource));
    }
}
