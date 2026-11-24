using LivingWorld.Domain.Ecology;
using LivingWorld.Domain.Fauna;
using LivingWorld.Domain.Flora;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Ecology;

/// <summary>Ciclo de vida base da fauna, diário (AD-135; antes por hora, com cria a cada hora): morte natural, fome, pasto
/// (herbívoro come planta e a planta perde estágio), reprodução sazonal em ninhadas com intervalo de gestação, e predação.
/// A população fica presa ao alimento: sem pasto a energia cai abaixo do limiar de cria e o animal morre de fome.
/// Roda com <c>Extraordinary.Enabled == false</c>; poderes modulam, não substituem.</summary>
[LivingCapability("FAUNA", Consumers = ["map.fauna", "map.flora"])]
public sealed class FaunaLifecycleSystem : ISimulationSystem
{
    public const string SystemName = "fauna-lifecycle";
    public const double PredationEnergyGain = 40;
    /// <summary>Teto leve: evita explosão O(n²) de reprodução em horizontes longos.</summary>
    public const int MaxAliveFauna = 400;

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        ApplyNaturalMortality(world, ctx);
        ApplyHunger(world, ctx);
        Graze(world, ctx);
        TryReproduce(world, ctx);
        TryPredate(world, ctx);
    }

    /// <summary>Morte por idade, doença ou acidente: chance diária = mortalidade anual da espécie / dias do ano.</summary>
    public static void ApplyNaturalMortality(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        double daysPerYear = world.Calendar.DaysPerMonth * world.Calendar.MonthsPerYear;
        long tick = ctx.CurrentTick;
        foreach (var animal in AliveById(world))
        {
            if (!rulesBySpecies.TryGetValue(animal.Species, out var rules) || rules.AnnualMortality <= 0) continue;
            if (ctx.DrawFor("fauna-mortality", unchecked(animal.Id.Value * 1_000_003L + tick)) < rules.AnnualMortality / daysPerYear)
                Kill(world, ctx, animal, WorldEventKind.Death);
        }
    }

    /// <summary>Herbívoro com fome pasta a planta mais próxima no raio (empate por id) que tenha pasto bastante para uma refeição:
    /// ganha energia e a planta perde estágio (rebrota depois). Sem planta por perto, nada: a fome continua.</summary>
    public static void Graze(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        if (!rulesBySpecies.Values.Any(r => r.Diet == AnimalSpeciesRules.FloraDiet && r.MealEnergy > 0)) return;
        long tick = ctx.CurrentTick;
        PlantGrid? plantAt = null;
        Dictionary<string, PlantSpeciesRules>? plantRules = null;
        foreach (var animal in AliveById(world))
        {
            if (!rulesBySpecies.TryGetValue(animal.Species, out var rules) || rules.Diet != AnimalSpeciesRules.FloraDiet || rules.MealEnergy <= 0)
                continue;
            if (animal.Energy.ValueAt(tick) > LazyNeed.Max - rules.MealEnergy) continue;

            plantAt ??= PlantGrid.Build(world);
            plantRules ??= FloraLifecycleSystem.IndexRules(world);
            int r = (int)Math.Floor(rules.ForageRadius);
            int cost = Math.Max(1, rules.GrazeStageCost);
            Plant? best = null;
            long bestForage = 0;
            double snowAccess = LivingWorld.Simulation.Climate.WeatherNow.GrazeAccess(world, animal.Position); // AD-180: neve cobre o pasto
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    var cell = new CellCoord(animal.Position.X + dx, animal.Position.Y + dy);
                    if (!plantAt.TryGet(cell, out var plantId) || world.FindPlant(plantId) is not { } plant) continue;
                    long forage = (long)(Forage(plant, plantRules) * snowAccess);
                    if (forage > bestForage || (forage == bestForage && forage > 0 && plant.Id.Value < best!.Id.Value))
                    {
                        best = plant;
                        bestForage = forage;
                    }
                }

            // pasto ralo (menos de um quarto de refeição) não segura o animal no lugar: ele sai à procura
            if (best is null || bestForage * 4 < cost)
            {
                if (rules.WandersWhenHungry) Wander(world, ctx, animal);
                if (best is null) continue;
            }

            long eaten = Math.Min(bestForage, cost);
            // AD-189: com a vegetação nua (inverno: erva parada, broto seco) a mesma bocada sustenta menos
            double nourishment = LivingWorld.Simulation.Climate.WeatherNow.ForageShare(world, animal.Position);
            world.ReplaceAnimal((world.FindAnimal(animal.Id) ?? animal) is var current
                ? current with { Energy = current.Energy.WithValue(current.Energy.ValueAt(tick) + rules.MealEnergy * eaten / (double)cost * nourishment, tick) }
                : animal);
            // a planta oferece no máximo o pasto de uma planta madura: estágio acumulado além disso não é comida
            world.ReplacePlant(best with { GrowthStage = (int)(Math.Min(best.GrowthStage, MaturityOf(best, plantRules)) - eaten) });
        }
    }

    private static int MaturityOf(Plant plant, Dictionary<string, PlantSpeciesRules> rules) =>
        rules.TryGetValue(plant.Species, out var r) ? Math.Max(1, r.MaturityStage) : Math.Max(1, plant.GrowthStage);

    /// <summary>Pasto que a planta oferece: o estágio, até o de uma planta madura.</summary>
    private static long Forage(Plant plant, Dictionary<string, PlantSpeciesRules> rules) =>
        Math.Max(0, Math.Min(plant.GrowthStage, MaturityOf(plant, rules)));

    /// <summary>Decai energia por espécie; energia 0 → morte + evento causal.</summary>
    public static void ApplyHunger(WorldState world, TickContext ctx)
    {
        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;

        foreach (var animal in AliveById(world))
        {
            if (!rulesBySpecies.TryGetValue(animal.Species, out var rules))
                continue;

            var energy = animal.Energy;
            if (Math.Abs(energy.DecayRatePerHour - rules.HungerDecayPerTick) > double.Epsilon)
                energy = energy.WithDecayRate(rules.HungerDecayPerTick, tick);

            if (energy.ValueAt(tick) > 0)
            {
                if (energy != animal.Energy)
                    world.ReplaceAnimal(animal with { Energy = energy });
                continue;
            }

            Kill(world, ctx, animal with { Energy = energy }, WorldEventKind.Starvation);
        }
    }

    /// <summary>Par elegível (mesma espécie, raio, energia) gera filhote próximo.</summary>
    public static void TryReproduce(WorldState world, TickContext ctx)
    {
        if (CountAlive(world) >= MaxAliveFauna)
            return;
        if (!world.AnimalSpeciesRules.Any(r => r.ReproduceProbability > 0))
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        int month = world.CurrentDate.Month;
        long hoursPerDay = world.Calendar.HoursPerDay;
        var alive = AliveById(world);
        var used = new HashSet<long>();
        // AD-167: quem pode ser par (energia e intervalo de gestação pelas regras da própria espécie) é o mesmo para todo a da
        // mesma espécie: conta uma vez por animal, não a cada par (a lista é a foto do começo do passo; o laço não a muda)
        var mate = new bool[alive.Count];
        for (int m = 0; m < alive.Count; m++)
            mate[m] = rulesBySpecies.TryGetValue(alive[m].Species, out var own)
                && alive[m].Energy.ValueAt(tick) >= own.ReproduceEnergyThreshold
                && !(alive[m].LastBirthTick is { } last && tick - last < own.GestationDays * hoursPerDay);

        for (int i = 0; i < alive.Count; i++)
        {
            var a = alive[i];
            if (used.Contains(a.Id.Value)) continue;
            if (!rulesBySpecies.TryGetValue(a.Species, out var rules)) continue;
            if (rules.ReproduceProbability <= 0 || !rules.BreedsInMonth(month)) continue;
            if (a.Energy.ValueAt(tick) < rules.ReproduceEnergyThreshold) continue;
            if (a.LastBirthTick is { } lastA && tick - lastA < rules.GestationDays * hoursPerDay) continue;

            for (int j = i + 1; j < alive.Count; j++)
            {
                if (!mate[j]) continue;
                var b = alive[j];
                if (used.Contains(b.Id.Value)) continue;
                if (!string.Equals(a.Species, b.Species, StringComparison.Ordinal)) continue;
                if (Chebyshev(a.Position, b.Position) > rules.ReproduceRadius) continue;

                double roll = ctx.DrawFor("fauna-reproduce", PairStreamId(a.Id.Value, b.Id.Value, tick));
                used.Add(a.Id.Value);
                used.Add(b.Id.Value);
                if (roll >= rules.ReproduceProbability) break;

                if (rules.GestationDays > 0)
                {
                    world.ReplaceAnimal((world.FindAnimal(a.Id) ?? a) with { LastBirthTick = tick });
                    world.ReplaceAnimal((world.FindAnimal(b.Id) ?? b) with { LastBirthTick = tick });
                }

                for (int k = 0; k < Math.Max(1, rules.LitterSize); k++)
                {
                    if (CountAlive(world) >= MaxAliveFauna) break;
                    var childPos = BirthPosition(world, ctx, a.Position, b.Position, a.Id.Value, b.Id.Value, tick + k);
                    var childId = world.NextAnimalIdAndAdvance();
                    var child = new Animal(
                        childId,
                        a.Species,
                        childPos,
                        true,
                        null,
                        LazyNeed.Initial(100, tick, rules.HungerDecayPerTick),
                        // a cria só cruza depois de um intervalo de gestação: período juvenil grosseiro, sem idade no animal
                        LastBirthTick: rules.GestationDays > 0 ? tick : null);
                    world.AddAnimal(child);
                    ctx.LogEvent(
                        WorldEventKind.Birth,
                        $"{a.Id.Value}|{b.Id.Value}|{childId.Value}",
                        sourceSystem: SystemName);
                }

                break;
            }
        }
    }

    private static CellCoord BirthPosition(
        WorldState world, TickContext ctx, CellCoord a, CellCoord b, long idA, long idB, long tick)
    {
        int midX = (a.X + b.X) / 2;
        int midY = (a.Y + b.Y) / 2;
        var candidates = new List<CellCoord>(9);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var cell = new CellCoord(midX + dx, midY + dy);
                if (world.Map.TryGetCell(cell, out _))
                    candidates.Add(cell);
            }

        if (candidates.Count == 0)
            return a;

        candidates.Sort((left, right) =>
        {
            int cmp = left.X.CompareTo(right.X);
            return cmp != 0 ? cmp : left.Y.CompareTo(right.Y);
        });
        int index = (int)(ctx.DrawFor("fauna-birth-pos", PairStreamId(idA, idB, tick)) * candidates.Count);
        if (index >= candidates.Count) index = candidates.Count - 1;
        return candidates[index];
    }

    /// <summary>Predador consome presa no raio; sem PredatorOf é no-op.</summary>
    public static void TryPredate(WorldState world, TickContext ctx)
    {
        if (!world.AnimalSpeciesRules.Any(r =>
                !string.IsNullOrEmpty(r.PredatorOf) && r.PredationProbability > 0))
            return;

        var rulesBySpecies = IndexRules(world);
        long tick = ctx.CurrentTick;
        var alive = AliveById(world);
        var eaten = new HashSet<long>();

        foreach (var predator in alive)
        {
            if (eaten.Contains(predator.Id.Value)) continue;
            if (!rulesBySpecies.TryGetValue(predator.Species, out var rules)) continue;
            if (string.IsNullOrEmpty(rules.PredatorOf) || rules.PredationProbability <= 0) continue;
            if (rules.HuntsOnlyWhenHungry && predator.Energy.ValueAt(tick) > LazyNeed.Max - PredationEnergyGain) continue;

            bool preyInRange = false;
            foreach (var prey in alive)
            {
                if (prey.Id.Value == predator.Id.Value) continue;
                if (eaten.Contains(prey.Id.Value)) continue;
                if (!string.Equals(prey.Species, rules.PredatorOf, StringComparison.Ordinal)) continue;
                if (Chebyshev(predator.Position, prey.Position) > rules.ReproduceRadius) continue;
                preyInRange = true;

                double roll = ctx.DrawFor("fauna-predate", PairStreamId(predator.Id.Value, prey.Id.Value, tick));
                if (roll >= rules.PredationProbability) continue;

                var currentPredator = world.FindAnimal(predator.Id) ?? predator;
                double gained = currentPredator.Energy.ValueAt(tick) + PredationEnergyGain;
                world.ReplaceAnimal(currentPredator with
                {
                    Energy = currentPredator.Energy.WithValue(gained, tick),
                });
                Kill(world, ctx, prey, WorldEventKind.Death);
                eaten.Add(prey.Id.Value);
                break;
            }

            if (!preyInRange && rules.WandersWhenHungry && world.FindAnimal(predator.Id) is { IsAlive: true } hungry)
                Wander(world, ctx, hungry);
        }
    }

    /// <summary>Um passo para uma célula vizinha do mapa, sorteada por (animal, dia): quem não acha comida sai à procura.</summary>
    private static void Wander(WorldState world, TickContext ctx, Animal animal)
    {
        Span<CellCoord> options = stackalloc CellCoord[8];
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = new CellCoord(animal.Position.X + dx, animal.Position.Y + dy);
                if (world.Map.TryGetCell(cell, out _)) options[count++] = cell;
            }

        if (count == 0) return;
        int pick = (int)(ctx.DrawFor("fauna-wander", unchecked(animal.Id.Value * 1_000_003L + ctx.CurrentTick)) * count);
        world.ReplaceAnimal(animal with { Position = options[Math.Min(pick, count - 1)] });
    }

    internal static void Kill(WorldState world, TickContext ctx, Animal animal, WorldEventKind kind)
    {
        if (!animal.IsAlive) return;
        world.ReplaceAnimal(animal with { IsAlive = false, DeathTick = ctx.CurrentTick });
        ctx.LogEvent(kind, animal.Id.Value.ToString(), sourceSystem: SystemName);
    }

    // As regras de espécie são imutáveis por mundo: indexa uma vez por instância da lista, não por tick.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        IReadOnlyList<AnimalSpeciesRules>, Dictionary<string, AnimalSpeciesRules>> RulesIndexCache = new();

    internal static Dictionary<string, AnimalSpeciesRules> IndexRules(WorldState world) =>
        RulesIndexCache.GetValue(world.AnimalSpeciesRules, rules => rules
            .GroupBy(r => r.Species, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal));

    /// <summary>Os vivos, na ordem do id (a lista da fauna já é ordenada por id: invariante do <see cref="WorldState"/>). AD-167: no
    /// lugar de <c>Where/OrderBy/ToList</c>, que ordenava de novo a lista inteira, mortos inclusive, cinco vezes por dia.</summary>
    private static List<Animal> AliveById(WorldState world)
    {
        var fauna = world.Fauna;
        var alive = new List<Animal>(Math.Min(fauna.Count, MaxAliveFauna + 16));
        for (int i = 0; i < fauna.Count; i++)
            if (fauna[i].IsAlive) alive.Add(fauna[i]);
        return alive;
    }

    private static int CountAlive(WorldState world)
    {
        var fauna = world.Fauna;
        int count = 0;
        for (int i = 0; i < fauna.Count; i++)
            if (fauna[i].IsAlive) count++;
        return count;
    }

    /// <summary>Por célula, a planta de menor id (a mesma conta de <c>GroupBy(posição).Min(id)</c>), numa grade densa do mapa (o pasto
    /// olha (2r+1)² células por animal com fome); célula fora do mapa vai a um dicionário. AD-167: antes, grupos e dicionário por dia.</summary>
    private sealed class PlantGrid
    {
        private readonly int _width, _height;
        private readonly long[] _lowest; // id + 1; 0 = sem planta
        private Dictionary<CellCoord, long>? _outside;

        private PlantGrid(int width, int height)
        {
            _width = Math.Max(0, width);
            _height = Math.Max(0, height);
            _lowest = new long[_width * _height];
        }

        public static PlantGrid Build(WorldState world)
        {
            var grid = new PlantGrid(world.Map.Width, world.Map.Height);
            var flora = world.Flora;
            for (int i = 0; i < flora.Count; i++)
            {
                var plant = flora[i];
                long id = plant.Id.Value + 1;
                var c = plant.Position;
                if ((uint)c.X < (uint)grid._width && (uint)c.Y < (uint)grid._height)
                {
                    ref long slot = ref grid._lowest[c.Y * grid._width + c.X];
                    if (slot == 0 || id < slot) slot = id;
                }
                else
                {
                    grid._outside ??= [];
                    if (!grid._outside.TryGetValue(c, out long known) || id < known) grid._outside[c] = id;
                }
            }
            return grid;
        }

        public bool TryGet(CellCoord c, out PlantId plant)
        {
            long id = (uint)c.X < (uint)_width && (uint)c.Y < (uint)_height
                ? _lowest[c.Y * _width + c.X]
                : _outside is not null && _outside.TryGetValue(c, out long o) ? o : 0;
            plant = new PlantId(id - 1);
            return id != 0;
        }
    }

    public static int Chebyshev(CellCoord a, CellCoord b) =>
        Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static long PairStreamId(long idA, long idB, long tick) =>
        unchecked(idA * 1_000_000_000L + idB * 1_000_000L + tick);
}
