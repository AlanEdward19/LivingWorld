using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ecology;

/// <summary>Ciclo de vida base da flora — avanço de estágio por temperatura/estação.
/// Roda com <c>Extraordinary.Enabled == false</c>; <c>flora.growth-rate</c> multiplica a taxa
/// de base, nunca a substitui. Produção deposita no workplace/CropBatch
/// existente; reprodução brota em espaço livre.</summary>
[LivingCapability("FLORA", Events = [WorldEventKind.PlantMatured], Consumers = ["map.flora"])]
public sealed class FloraLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "flora-lifecycle";
    /// <summary>Teto leve: evita crescimento ilimitado em horizontes longos.</summary>
    public const int MaxAliveFlora = 800;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Hourly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var state = States.GetValue(world, _ => new FastState());
        long tick = ctx.CurrentTick;
        if (state.TrySkip(world, tick))
            return;

        state.Flush(world);
        AdvanceStage(world, ctx);
        TryReproduce(world, ctx);
        state.Rebuild(world, tick);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldState, FastState> States = new();

    /// <summary>Avanço adiado. Entre eventos de planta (maturidade/morte) e enquanto o clima, a lista de
    /// plantas e os poderes não mudam, cada planta ganha o mesmo <c>delta</c> a cada tick — então o tick
    /// só conta, e <c>estágio += delta × ticks</c> é aplicado quando alguém lê a flora (ver
    /// <see cref="WorldState.Flora"/>) ou no tick em que um evento passa a ser possível, que roda o caminho
    /// normal inteiro. O resultado em qualquer ponto observável é idêntico ao do avanço tick a tick.</summary>
    private sealed class FastState
    {
        private const long NoEvent = long.MaxValue;

        private bool _valid;
        private long _floraVersion;
        private long _temperatureVersion;
        private long _validFrom;
        private long _validUntil;
        private int[] _deltas = [];
        private long _horizon;   // ticks até o primeiro evento possível (contados desde a última materialização)
        private int _pending;    // ticks já contados e ainda não aplicados às plantas
        private Action? _flush;

        public bool TrySkip(WorldState world, long tick)
        {
            if (!_valid
                || world.FloraMutationVersion != _floraVersion
                || world.TemperatureAdjustmentsVersion != _temperatureVersion
                || tick < _validFrom || tick >= _validUntil
                || world.Extraordinary.Enabled
                || !ReproductionIsNoOp(world))
                return false;

            // O próximo incremento seria o de índice _pending + 1; se ele já alcança o horizonte, vira tick normal.
            if (_pending + 1 >= _horizon)
                return false;

            _pending++;
            world.FloraPendingFlush = _flush ??= () => Flush(world);
            return true;
        }

        /// <summary>Aplica os ticks contados. Chamado pela leitura da flora e antes de um tick normal.</summary>
        public void Flush(WorldState world)
        {
            world.FloraPendingFlush = null;
            if (_pending == 0) return;
            int ticks = _pending;
            _pending = 0;
            _horizon = _horizon == NoEvent ? NoEvent : _horizon - ticks;
            world.ApplyFloraStageDeltas(_deltas, ticks);
        }

        public void Rebuild(WorldState world, long tick)
        {
            _valid = false;
            if (world.Extraordinary.Enabled || !ReproductionIsNoOp(world))
                return;

            var rulesBySpecies = IndexRules(world);
            var field = EnvironmentTemperatureMechanic.FieldAt(world, tick);
            var plants = world.Flora;
            var deltas = new int[plants.Count];
            long horizon = NoEvent;

            for (int i = 0; i < deltas.Length; i++)
            {
                var plant = plants[i];
                if (!rulesBySpecies.TryGetValue(plant.Species, out var rules))
                    continue;

                int delta = (int)Math.Floor(BaseGrowthRate(field.At(plant.Position), rules) * 1.0);
                deltas[i] = delta;
                if (delta == 0) continue;

                long ticksToEvent;
                if (delta > 0)
                {
                    if (plant.GrowthStage >= rules.MaturityStage) continue; // já madura: sem novo evento
                    ticksToEvent = ((long)rules.MaturityStage - plant.GrowthStage + delta - 1) / delta;
                }
                else
                {
                    // morre no primeiro tick em que estágio + k × delta < 0
                    ticksToEvent = plant.GrowthStage < 0 ? 1 : plant.GrowthStage / -(long)delta + 1;
                }

                horizon = Math.Min(horizon, ticksToEvent);
            }

            _deltas = deltas;
            _horizon = horizon;
            _pending = 0;
            _floraVersion = world.FloraMutationVersion;
            _temperatureVersion = world.TemperatureAdjustmentsVersion;
            _validFrom = tick;
            _validUntil = field.ValidUntil;
            _valid = true;
        }

        /// <summary>Reprodução não faz nada: lotada (teto) ou nenhuma espécie com chance de brotar.</summary>
        private static bool ReproductionIsNoOp(WorldState world)
        {
            if (world.FloraCount >= MaxAliveFlora) return true;
            foreach (var rules in world.PlantSpeciesRules)
                if (rules.ReproduceProbability > 0) return false;
            return true;
        }
    }

    /// <summary>Avança (ou reverte) estágio conforme temperatura efetiva;
    /// poder multiplica a taxa de base. Ao atingir maturidade, deposita yield no workplace
    /// da célula — nunca cria estoque keyed por <see cref="Plant"/>.</summary>
    public static void AdvanceStage(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        var temperature = EnvironmentTemperatureMechanic.FieldAt(world, tick);
        // Quase toda planta repete a espécie da anterior (mesma instância de string).
        string? lastSpecies = null;
        PlantSpeciesRules? lastRules = null;

        bool powersActive = world.Extraordinary.Enabled;
        var snapshot = world.Flora.ToArray();
        int removed = 0;

        for (int i = 0; i < snapshot.Length; i++)
        {
            var plant = snapshot[i];
            PlantSpeciesRules? rules;
            if (ReferenceEquals(plant.Species, lastSpecies))
                rules = lastRules;
            else
            {
                rulesBySpecies.TryGetValue(plant.Species, out rules);
                lastSpecies = plant.Species;
                lastRules = rules;
            }

            if (rules is null)
                continue;

            double baseRate = BaseGrowthRate(temperature.At(plant.Position), rules);
            double multiplier = powersActive ? FloraMechanic.GrowthRateMultiplier(world, plant) : 1;
            int delta = (int)Math.Floor(baseRate * multiplier);
            if (delta == 0)
                continue;

            int nextStage = plant.GrowthStage + delta;
            if (nextStage < 0)
            {
                Kill(world, ctx, plant);
                removed++;
                continue;
            }

            world.ReplacePlantAt(i - removed, plant with { GrowthStage = nextStage });
            if (plant.GrowthStage < rules.MaturityStage && nextStage >= rules.MaturityStage)
            {
                ctx.LogEvent(
                    WorldEventKind.PlantMatured,
                    plant.Id.Value.ToString(),
                    sourceSystem: SystemName);
                DepositYield(world, ctx, plant, rules);
            }
        }
    }

    /// <summary>Planta madura com espaço livre compatível brota nova planta
    /// (RNG semeado por stream).</summary>
    public static void TryReproduce(WorldState world, TickContext ctx)
    {
        if (world.Flora.Count >= MaxAliveFlora)
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        HashSet<CellCoord>? occupied = null;

        foreach (var plant in world.Flora.ToArray())
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

            occupied ??= world.Flora.Select(p => p.Position).ToHashSet();
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
        WorldState world, Plant plant, PlantSpeciesRules rules, long currentTick) =>
        BaseGrowthRate(EnvironmentTemperatureMechanic.FieldAt(world, currentTick).At(plant.Position), rules);

    private static double BaseGrowthRate(float temp, PlantSpeciesRules rules)
    {
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

    internal static void DepositYield(WorldState world, TickContext ctx, Plant plant, PlantSpeciesRules rules)
    {
        if (!world.EconomyRules.Enabled)
            return;
        // o ano de safra da região multiplica a colheita (weather-drought): seca corta, fartura soma; sem clima vale 1000
        long yield = (long)Math.Floor(rules.YieldPerMaturePlant * world.ClimateFactorAt(plant.Position, ctx.CurrentTick) / 1000.0);
        if (yield <= 0)
            return;

        var resource = new ResourceType(rules.CropResourceId);
        var workplace = world.Workplaces
            .Where(w => w.Location == plant.Position)
            .OrderBy(w => w.Id.Value)
            .FirstOrDefault();
        if (workplace is null)
            return;

        long lost = workplace.Deposit(resource, yield, world.EconomyRules);
        if (lost > 0)
            ctx.LogEvent(
                WorldEventKind.ResourceLost,
                $"{workplace.Id.Value}|{rules.CropResourceId}|{lost}", sourceSystem: SystemName);
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

    /// <summary>Remove do hot e grava resumo frio imediato (Plant não carrega
    /// DeathTick; fauna usa idade via ColdArchiveSystem).</summary>
    internal static void Kill(WorldState world, TickContext ctx, Plant plant)
    {
        world.ColdArchive.ArchivePlantOnDeath(plant, ctx.CurrentTick);
        world.RemovePlant(plant.Id);
        ctx.LogEvent(WorldEventKind.Death, plant.Id.Value.ToString(), sourceSystem: SystemName);
    }

    // As regras de espécie são imutáveis por mundo: indexa uma vez por instância da lista, não por tick.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        IReadOnlyList<PlantSpeciesRules>, Dictionary<string, PlantSpeciesRules>> RulesIndexCache = new();

    internal static Dictionary<string, PlantSpeciesRules> IndexRules(WorldState world) =>
        RulesIndexCache.GetValue(world.PlantSpeciesRules, rules => rules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal));
}
