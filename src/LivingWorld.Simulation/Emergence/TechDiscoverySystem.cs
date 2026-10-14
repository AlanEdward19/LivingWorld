using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Emergence;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Market;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Emergence;

/// <summary>Tecnologia como composição de eixos (emergence-first-slice, ADR-0013), <c>Yearly</c>. Numa cidade onde a pressão de
/// trajeto longo, a habilidade de quem vive nela e o estoque de recurso coincidem, abre um protótipo: gasta o recurso (registrado
/// como consumido, então a conservação fecha) e agenda a conclusão no <see cref="EventScheduler"/>. O vetor de eixos nasce de
/// pressão, habilidade e estoque mais um sorteio do stream (cidade, ano). Ao concluir, o limiar de novidade decide entre variação
/// de uma tecnologia da cultura e entidade nova; a adoção espalha por familiaridade entre cidades da mesma cultura. Sem nome:
/// a tecnologia existe e já encurta o trajeto de mercado de quem a adotou (<see cref="Mobility"/>).</summary>
[LivingCapability("EMERGENCE", Events = [WorldEventKind.TechnologyDiscovered, WorldEventKind.TechnologyAdopted], Consumers = ["behavior.travel"])]
public sealed class TechDiscoverySystem : ISimulationSystem
{
    public const string SystemName = "tech-discovery";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Yearly;

    public void Tick(WorldState world, TickContext ctx)
    {
        var rules = world.EmergenceRules;
        if (!rules.Enabled) return;

        var residents = AliveResidentsByCity(world);
        var pending = PendingPrototypeCities(world);
        var marketIndex = MarketIndex.BuildForTick(world);
        long year = ctx.CurrentTick / world.Calendar.HoursPerYear;

        foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
        {
            if (pending.Contains(city.Id) || !residents.TryGetValue(city.Id, out var people)) continue;

            var pressure = Pressure(world, city.Id, marketIndex, rules);
            if (pressure.Affected < rules.MinAffectedHouseholds) continue;

            double bestSkill = 0;
            foreach (var npc in people)
                foreach (var value in npc.Skills.Values.Values)
                    if (value > bestSkill) bestSkill = value;
            if (bestSkill < rules.MinSkill) continue;

            var resource = new ResourceType(rules.PrototypeResourceId);
            long stock = city.Stock.GetValueOrDefault(resource);
            if (stock < rules.PrototypeResourceCost) continue;

            if (rules.PrototypeResourceCost > 0)
            {
                if (!city.WithdrawStock(resource, rules.PrototypeResourceCost).IsSuccess) continue;
                world.RecordResourceConsumed(resource, rules.PrototypeResourceCost);
            }

            var vector = Compose(rules, pressure.MeanTicks, bestSkill, stock, ctx.Rng($"emergence-{city.Id.Value}-{year}"));
            var culture = people[0].Culture;
            ctx.ScheduleEvent(
                ctx.CurrentTick + rules.PrototypeTicks, SystemName,
                $"{city.Id.Value}|{culture.Id.ToString(CultureInfo.InvariantCulture)}|{vector.Key}");
        }

        Spread(world, ctx, residents);
    }

    public void HandleEvent(WorldState world, TickContext ctx, ScheduledEvent evt)
    {
        var rules = world.EmergenceRules;
        if (!rules.Enabled || evt.Payload is null) return;

        var parts = evt.Payload.Split('|');
        if (parts.Length != 3) return;
        var cityId = new CityId(Guid.Parse(parts[0]));
        var culture = new CultureId(int.Parse(parts[1], CultureInfo.InvariantCulture));
        var vector = new TechnologyVector(parts[2].Split('.').Select(a => int.Parse(a, CultureInfo.InvariantCulture)).ToArray());

        string? variationOf = null;
        int nearest = int.MaxValue;
        foreach (var known in world.Technologies)
        {
            if (known.Culture != culture || known.Category != EmergenceRules.MobilityCategory) continue;
            int distance = known.Vector.DistanceTo(vector);
            if (distance < nearest) { nearest = distance; variationOf = known.Vector.Key; }
        }
        if (nearest >= rules.NoveltyThreshold) variationOf = null;

        world.AddTechnology(new Technology(
            culture, EmergenceRules.MobilityCategory, vector, ctx.CurrentTick, cityId, variationOf, [cityId]));
        ctx.LogEvent(
            WorldEventKind.TechnologyDiscovered, $"{cityId.Value}|{culture.Id}|{vector.Key}|{variationOf ?? ""}",
            sourceSystem: SystemName);
    }

    private static TechnologyVector Compose(EmergenceRules rules, double meanTicks, double bestSkill, long stock, WorldRng rng)
    {
        int max = rules.AxisMax;
        int pressureLevel = (int)Math.Min(max, meanTicks * max / (4.0 * rules.PressureTravelTicks));
        int skillLevel = (int)Math.Min(max, bestSkill * max / 100.0);
        int stockLevel = rules.PrototypeResourceCost == 0
            ? max / 2
            : (int)Math.Min(max, stock * max / (4.0 * rules.PrototypeResourceCost));
        int[] levels = [pressureLevel, skillLevel, stockLevel];

        var axes = new int[rules.AxisCount];
        for (int i = 0; i < axes.Length; i++)
        {
            int jitter = (int)Math.Floor(rng.NextDouble() * 5) - 2;
            axes[i] = Math.Clamp(levels[i % levels.Length] + jitter, 0, max);
        }
        return new TechnologyVector(axes);
    }

    private readonly record struct PressureReading(int Affected, double MeanTicks);

    /// <summary>Domicílios da cidade cujo trajeto até o mercado (já com a mobilidade adotada) passa do limiar.</summary>
    private static PressureReading Pressure(WorldState world, CityId city, MarketIndex marketIndex, EmergenceRules rules)
    {
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        var water = new ResourceType(world.EconomyRules.WaterResourceId);
        int bonus = Mobility.BonusPct(world, city);
        int affected = 0;
        long total = 0;
        foreach (var household in world.Households)
        {
            if (household.City != city) continue;
            var market = marketIndex.NearestSelling(household.Location, food, water);
            if (market is null) continue;

            long ticks = Mobility.Apply(TravelResolution.TicksBetween(world.Map, household.Location, market.Location), bonus);
            if (ticks < rules.PressureTravelTicks) continue;
            affected++;
            total += ticks;
        }
        return new PressureReading(affected, affected == 0 ? 0 : (double)total / affected);
    }

    private static Dictionary<CityId, List<LivingWorld.Domain.Population.Npc>> AliveResidentsByCity(WorldState world)
    {
        var byCity = new Dictionary<CityId, List<LivingWorld.Domain.Population.Npc>>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive) continue;
            if (!byCity.TryGetValue(npc.City, out var list)) byCity[npc.City] = list = [];
            list.Add(npc);
        }
        return byCity;
    }

    private static HashSet<CityId> PendingPrototypeCities(WorldState world)
    {
        var cities = new HashSet<CityId>();
        foreach (var evt in world.Scheduler.Snapshot())
            if (evt.SystemName == SystemName && evt.Payload is { } payload)
                cities.Add(new CityId(Guid.Parse(payload.Split('|')[0])));
        return cities;
    }

    /// <summary>Uma cidade da mesma cultura adota a tecnologia quando alguém dela tem vínculo (confiança + afeto) forte com
    /// alguém de uma cidade que já a usa.</summary>
    private static void Spread(
        WorldState world, TickContext ctx, Dictionary<CityId, List<LivingWorld.Domain.Population.Npc>> residents)
    {
        var rules = world.EmergenceRules;
        for (int index = 0; index < world.Technologies.Count; index++)
        {
            var tech = world.Technologies[index];
            if (tech.IsVariation) continue;

            var adopters = new HashSet<CityId>(tech.AdoptedBy);
            var newcomers = new List<CityId>();
            foreach (var city in world.ActiveCities().OrderBy(c => c.Id.Value))
            {
                if (adopters.Contains(city.Id) || !residents.TryGetValue(city.Id, out var people)) continue;
                if (people[0].Culture != tech.Culture) continue;
                if (HasTieTo(world, people, adopters, rules.SpreadAffinity)) newcomers.Add(city.Id);
            }
            if (newcomers.Count == 0) continue;

            world.ReplaceTechnology(index, tech with { AdoptedBy = [.. tech.AdoptedBy, .. newcomers] });
            foreach (var city in newcomers)
                ctx.LogEvent(
                    WorldEventKind.TechnologyAdopted, $"{city.Value}|{tech.Culture.Id}|{tech.Vector.Key}",
                    sourceSystem: SystemName);
        }
    }

    private static bool HasTieTo(
        WorldState world, List<LivingWorld.Domain.Population.Npc> people, HashSet<CityId> adopters, double affinity)
    {
        foreach (var npc in people)
        {
            foreach (var target in world.RelationshipTargetsOf(npc.Id))
            {
                if (world.FindNpc(target) is not { } other || !adopters.Contains(other.City)) continue;
                var tie = world.Relationships[new RelationshipKey(npc.Id, target)];
                if (tie.Trust + tie.Affection >= affinity) return true;
            }
        }
        return false;
    }
}
