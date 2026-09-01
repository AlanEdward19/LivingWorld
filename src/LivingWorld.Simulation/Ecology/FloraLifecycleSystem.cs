using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Ciclo de vida base da flora — avanço de estágio por temperatura/estação (Fase 16.4).
/// Roda com <c>Extraordinary.Enabled == false</c>; <c>flora.growth-rate</c> multiplica a taxa
/// de base, nunca a substitui (REALISM-07/08/11). Produção deposita no workplace/CropBatch
/// existente; reprodução brota em espaço livre (REALISM-09/10).</summary>
public sealed class FloraLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "flora-lifecycle";
    /// <summary>Teto leve (REALISM-19): evita crescimento ilimitado em horizontes longos.</summary>
    public const int MaxAliveFlora = 800;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        AdvanceStage(world, ctx);
        TryReproduce(world, ctx);
    }

    /// <summary>REALISM-07/08/11: avança (ou reverte) estágio conforme temperatura efetiva;
    /// poder multiplica a taxa de base. Ao atingir maturidade, deposita yield no workplace
    /// da célula (REALISM-09) — nunca cria estoque keyed por <see cref="Plant"/>.</summary>
    public static void AdvanceStage(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;

        foreach (var plant in world.Flora.OrderBy(p => p.Id.Value).ToList())
        {
            if (!rulesBySpecies.TryGetValue(plant.Species, out var rules))
                continue;

            double baseRate = BaseGrowthRate(world, plant, rules, tick);
            double multiplier = FloraMechanic.GrowthRateMultiplier(world, plant);
            int delta = (int)Math.Floor(baseRate * multiplier);
            if (delta == 0)
                continue;

            int nextStage = plant.GrowthStage + delta;
            if (nextStage < 0)
            {
                Kill(world, ctx, plant);
                continue;
            }

            world.ReplacePlant(plant with { GrowthStage = nextStage });
            if (plant.GrowthStage < rules.MaturityStage && nextStage >= rules.MaturityStage)
            {
                ctx.LogEvent(
                    WorldEventKind.PlantMatured,
                    plant.Id.Value.ToString(),
                    sourceSystem: SystemName);
                DepositYield(world, plant, rules);
            }
        }
    }

    /// <summary>REALISM-10: planta madura com espaço livre compatível brota nova planta
    /// (RNG semeado por stream).</summary>
    public static void TryReproduce(WorldState world, TickContext ctx)
    {
        if (world.Flora.Count >= MaxAliveFlora)
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        var occupied = world.Flora
            .Select(p => p.Position)
            .ToHashSet();

        foreach (var plant in world.Flora.OrderBy(p => p.Id.Value).ToList())
        {
            if (!rulesBySpecies.TryGetValue(plant.Species, out var rules))
                continue;
            if (plant.GrowthStage < rules.MaturityStage)
                continue;
            if (rules.ReproduceProbability <= 0)
                continue;

            double roll = ctx.StreamFor("flora-reproduce", unchecked(plant.Id.Value * 1_000_000L + tick))
                .NextDouble();
            if (roll >= rules.ReproduceProbability)
                continue;

            var freeCells = FreeCompatibleCells(world, plant.Position, rules.ReproduceRadius, occupied);
            if (freeCells.Count == 0)
                continue;

            int index = (int)(ctx.StreamFor("flora-sprout-pos", unchecked(plant.Id.Value * 1_000_000L + tick))
                .NextDouble()
                * freeCells.Count);
            if (index >= freeCells.Count) index = freeCells.Count - 1;
            var sproutPos = freeCells[index];
            var sproutId = world.NextPlantIdAndAdvance();
            var sprout = new Plant(sproutId, plant.Species, sproutPos, GrowthStage: 0);
            world.AddPlant(sprout);
            occupied.Add(sproutPos);
            ctx.LogEvent(
                WorldEventKind.Birth,
                $"{plant.Id.Value}|{sproutId.Value}",
                sourceSystem: SystemName);
        }
    }

    /// <summary>Taxa de base: positiva dentro da faixa (escala com conforto térmico);
    /// negativa fora da faixa (reverte — nunca avança normalmente).</summary>
    public static double BaseGrowthRate(
        WorldState world, Plant plant, PlantSpeciesRules rules, long currentTick)
    {
        float temp = EnvironmentTemperatureMechanic.EffectiveTemperature(
            world, plant.Position, currentTick);

        if (temp < rules.MinToleratedTemp || temp > rules.MaxToleratedTemp)
            return -1;

        float mid = (rules.MinToleratedTemp + rules.MaxToleratedTemp) / 2f;
        float half = (rules.MaxToleratedTemp - rules.MinToleratedTemp) / 2f;
        if (half <= 0)
            return 1;

        double comfort = 1.0 - Math.Abs(temp - mid) / half;
        // 1.0 na borda da faixa, 3.0 no centro — estações distintas mudam o floor da taxa.
        return 1.0 + 2.0 * Math.Clamp(comfort, 0, 1);
    }

    internal static void DepositYield(WorldState world, Plant plant, PlantSpeciesRules rules)
    {
        if (!world.EconomyRules.Enabled)
            return;
        long yield = (long)Math.Floor(rules.YieldPerMaturePlant);
        if (yield <= 0)
            return;

        var resource = new ResourceType(rules.CropResourceId);
        var workplace = world.Workplaces
            .Where(w => w.Location == plant.Position)
            .OrderBy(w => w.Id.Value)
            .FirstOrDefault();
        if (workplace is null)
            return;

        workplace.Deposit(resource, yield, world.EconomyRules);
        world.RecordResourceProduced(resource, yield);
    }

    internal static List<CellCoord> FreeCompatibleCells(
        WorldState world, CellCoord origin, double radius, HashSet<CellCoord> occupied)
    {
        int r = (int)Math.Floor(radius);
        var cells = new List<CellCoord>();
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > r) continue;
                var cell = new CellCoord(origin.X + dx, origin.Y + dy);
                if (!world.Map.TryGetCell(cell, out _)) continue;
                if (occupied.Contains(cell)) continue;
                cells.Add(cell);
            }

        cells.Sort((a, b) =>
        {
            int cmp = a.X.CompareTo(b.X);
            return cmp != 0 ? cmp : a.Y.CompareTo(b.Y);
        });
        return cells;
    }

    /// <summary>REALISM-21: remove do hot e grava resumo frio imediato (Plant não carrega
    /// DeathTick — contrato T7; fauna usa idade via ColdArchiveSystem).</summary>
    internal static void Kill(WorldState world, TickContext ctx, Plant plant)
    {
        world.ColdArchive.ArchivePlantOnDeath(plant, ctx.CurrentTick);
        world.RemovePlant(plant.Id);
        ctx.LogEvent(WorldEventKind.Death, plant.Id.Value.ToString(), sourceSystem: SystemName);
    }

    internal static Dictionary<string, PlantSpeciesRules> IndexRules(WorldState world) =>
        world.PlantSpeciesRules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
}
