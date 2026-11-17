using System.Collections.Concurrent;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Cities.Spatial;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cognition;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Api.Visual.Observable;

/// <summary>Índices de uma leitura (AD-170): montados uma vez por projeção de escopo, nunca guardados no mundo.</summary>
public sealed class ObservableContext
{
    public WorldState World { get; }
    public long Tick { get; }
    public PresentationNames Names { get; }
    private readonly Dictionary<long, Infection> _infections = new();
    private readonly Dictionary<long, List<CarriedLoad>> _carried = new();
    private readonly Dictionary<long, long> _held = new();
    private readonly Dictionary<long, long> _holderByItem = new();
    private readonly Dictionary<long, CraftedItem> _items = new();
    private readonly Dictionary<int, Design> _designs = new();

    public ObservableContext(WorldState world, PresentationNames? names = null)
    {
        World = world;
        Tick = world.CurrentDate.TotalHours;
        Names = names ?? PresentationNames.None;
        foreach (var infection in world.Infections)
            if (infection.EndTick > Tick && !_infections.ContainsKey(infection.Npc.Value))
                _infections[infection.Npc.Value] = infection;
        if (world.Invention is not { } ledger) return;
        foreach (var load in ledger.Carried)
            (_carried.TryGetValue(load.NpcId, out var list) ? list : _carried[load.NpcId] = []).Add(load);
        if (ledger.Held is { } held)
            foreach (var h in held)
            {
                _held[h.NpcId] = h.ItemId;
                _holderByItem[h.ItemId] = h.NpcId;
            }
        foreach (var item in ledger.Items) _items[item.Id] = item;
        foreach (var design in ledger.Designs) _designs[design.Id] = design;
    }

    public Infection? InfectionOf(long npc) => _infections.GetValueOrDefault(npc);
    public IReadOnlyList<CarriedLoad> CarriedOf(long npc) => _carried.TryGetValue(npc, out var list) ? list : [];
    public long? HeldOf(long npc) => _held.TryGetValue(npc, out long item) ? item : null;
    public long? HolderOf(long item) => _holderByItem.TryGetValue(item, out long npc) ? npc : null;
    public CraftedItem? Item(long id) => _items.TryGetValue(id, out var item) ? item : null;
    public Design? DesignOf(int id) => _designs.GetValueOrDefault(id);
    public IEnumerable<CraftedItem> Items => _items.Values;

    private Dictionary<CellCoord, Workplace>? _farms;

    /// <summary>A oficina na célula de uma lavoura (a fazenda dela), a de menor id.</summary>
    public Workplace? FarmAt(CellCoord plot)
    {
        _farms ??= World.Workplaces.OrderBy(w => w.Id.Value).GroupBy(w => w.Location).ToDictionary(g => g.Key, g => g.First());
        return _farms.GetValueOrDefault(plot);
    }
}

/// <summary>Monta os registros do observável a partir do estado do motor. Só leitura, sem RNG, sem mudar ordem de nada.</summary>
public static class ObservableProjector
{
    private static readonly string[] SeasonKeys = ["winter", "spring", "summer", "autumn"];
    private static readonly ConcurrentDictionary<(long, int, int), BuildingPlan> Plans = new();

    public static NpcObservable Npc(ObservableContext ctx, Npc npc)
    {
        var world = ctx.World;
        int age = npc.AgeYears(world.CurrentDate);
        var affect = PsycheMechanic.AffectOf(world, npc, ctx.Tick);
        var emotions = new EmotionsVisual(
            Tenth(affect.Grief), Tenth(affect.Fear), Tenth(affect.Anger), Tenth(affect.Joy), Tenth(affect.Shame), Tenth(affect.Loneliness));
        var carried = ctx.CarriedOf(npc.Id.Value)
            .Select(load => new CarriedVisual(load.ResourceId, load.Quantity, ctx.Names.Resource(load.ResourceId))).ToArray();
        HeldObjectVisual? held = ctx.HeldOf(npc.Id.Value) is { } itemId
            ? new HeldObjectVisual(itemId, ctx.Item(itemId)?.DesignId ?? 0)
            : null;
        IllnessVisual? illness = ctx.InfectionOf(npc.Id.Value) is { } infection
            ? new IllnessVisual(infection.Disease, infection.Symptomatic, infection.Bedridden)
            : null;
        double growth = npc.AdultHeight > 0 ? Math.Clamp(npc.Height / npc.AdultHeight, 0, 1) : 1;
        return new NpcObservable(
            npc.Name, npc.Sex.ToString(),
            age, world.LifeStageRules.LifeStageOf(age).ToString(),
            npc.Profession.Id, ctx.Names.Profession(npc.Profession.Id),
            (int)Math.Round(npc.Height * 100), Math.Round(growth, 2),
            PregnancyOf(world, npc, ctx.Tick), illness, emotions, Dominant(emotions), emotions.Grief >= 0.1,
            new ValueList<CarriedVisual>(carried), held);
    }

    private static PregnancyVisual? PregnancyOf(WorldState world, Npc npc, long tick)
    {
        if (npc.Reproduction?.Pregnancy is not { } pregnancy) return null;
        int hpd = world.Calendar.HoursPerDay;
        long shows = pregnancy.ConceivedTick + (long)world.FamilyRules.Unions.Intimacy.ShowsAfterDays * hpd;
        if (tick < shows || tick >= pregnancy.EndTick) return null;
        double fraction = (tick - pregnancy.ConceivedTick) / (double)Math.Max(1L, (long)pregnancy.GestationDays * hpd);
        return new PregnancyVisual(Math.Round(Math.Clamp(fraction, 0, 1) * 20) / 20, Math.Max(1, pregnancy.Sexes.Length));
    }

    private static string? Dominant(EmotionsVisual e)
    {
        (string Name, double Value)[] all =
            [("grief", e.Grief), ("fear", e.Fear), ("anger", e.Anger), ("joy", e.Joy), ("shame", e.Shame), ("loneliness", e.Loneliness)];
        var top = all.MaxBy(x => x.Value);
        return top.Value >= 0.1 ? top.Name : null;
    }

    /// <summary>Densidade por cidade para o escopo Mundo, numa passada pelos NPCs.</summary>
    public static Dictionary<CityId, CityDensityVisual> Densities(WorldState world, IReadOnlyDictionary<CityId, CityBounds> bounds)
    {
        var inside = new Dictionary<CityId, (int Count, Dictionary<int, int> Actions)>();
        foreach (var npc in world.Npcs)
        {
            if (!npc.IsAlive || !bounds.TryGetValue(npc.City, out var b) || NpcScopeResolver.Resolve(npc, b).Kind != NpcScopeKind.City)
                continue;
            if (!inside.TryGetValue(npc.City, out var entry)) entry = (0, new Dictionary<int, int>());
            int key = npc.CurrentAction is { } action ? (int)action : -1;
            entry.Actions[key] = entry.Actions.GetValueOrDefault(key) + 1;
            inside[npc.City] = (entry.Count + 1, entry.Actions);
        }

        var result = new Dictionary<CityId, CityDensityVisual>();
        foreach (var city in world.ActiveCities())
        {
            var (count, actions) = inside.TryGetValue(city.Id, out var e) ? e : (0, new Dictionary<int, int>());
            var top = actions.OrderByDescending(a => a.Value).ThenBy(a => a.Key).Take(4)
                .Select(a => new ActionCount(a.Key < 0 ? null : (ActionType)a.Key, a.Value)).ToArray();
            result[city.Id] = new CityDensityVisual(count, city.PoolNpcIds.Count, new ValueList<ActionCount>(top),
                Half(EnvironmentTemperatureMechanic.EffectiveTemperature(world, city.Location, world.CurrentDate.TotalHours)));
        }

        return result;
    }

    public static BuildingPlan Plan(Building building, int orientation)
    {
        if (Plans.Count > 50_000) Plans.Clear();
        return Plans.GetOrAdd((building.Id.Value, building.BuildingTypeId, orientation), key =>
        {
            var layout = BuildingLayoutGenerator.Generate(new BuildingId(key.Item1), key.Item2, key.Item3);
            return new BuildingPlan(layout.Floors, new ValueList<FootprintCell>(layout.FloorLayouts[0].Cells.ToArray()));
        });
    }

    /// <summary>Função, dono, moradores, trabalhadores e estoque resumido dos prédios visíveis de uma cidade.</summary>
    public static IReadOnlyList<BuildingStateVisual> BuildingStates(ObservableContext ctx, IReadOnlyList<Building> visible)
    {
        if (visible.Count == 0) return [];
        var world = ctx.World;
        var ids = visible.Select(b => b.Id).ToHashSet();
        var households = world.Households.Where(h => h.Dwelling is { } d && ids.Contains(d))
            .GroupBy(h => h.Dwelling!.Value).ToDictionary(g => g.Key, g => g.OrderBy(h => h.Id.Value).ToList());
        var workplaces = world.Workplaces.Where(w => w.Building is { } b && ids.Contains(b))
            .GroupBy(w => w.Building!.Value).ToDictionary(g => g.Key, g => g.OrderBy(w => w.Id.Value).First());
        var recipes = world.CityCatalog.BuildingRecipes;
        return visible.Select(building =>
        {
            var homes = households.GetValueOrDefault(building.Id) ?? [];
            var workplace = workplaces.GetValueOrDefault(building.Id);
            recipes.TryGetValue(building.BuildingTypeId, out var recipe);
            var function = new List<string>();
            if (recipe?.Lodging is not null) function.Add("lodging");
            else if (homes.Count > 0 || recipe?.HousingCapacityProvided > 0) function.Add("dwelling");
            if (workplace is not null) function.Add("workplace");
            if (function.Count == 0) function.Add("structure");
            var stock = workplace is null
                ? []
                : workplace.Stock.Where(s => s.Value > 0).OrderByDescending(s => s.Value).ThenBy(s => s.Key.Id).Take(5)
                    .Select(s => new StockLine(s.Key.Id, Bucket(s.Value), ctx.Names.Resource(s.Key.Id))).ToArray();
            return new BuildingStateVisual(
                building.Id.Value, building.BuildingTypeId, ctx.Names.BuildingType(building.BuildingTypeId), string.Join(',', function),
                workplace?.Id.Value, workplace?.LocationType.Id,
                workplace is null ? null : ctx.Names.LocationType(workplace.LocationType.Id),
                workplace?.Owner?.Value,
                new ValueList<long>(homes.Select(h => h.Id.Value).ToArray()),
                new ValueList<long>(homes.SelectMany(h => h.Members).Select(m => m.Value).OrderBy(v => v).ToArray()),
                new ValueList<long>(workplace?.Employees.Select(e => e.Value).OrderBy(v => v).ToArray() ?? []),
                new ValueList<StockLine>(stock),
                homes.Sum(h => h.Stock.Values.Sum()),
                workplace?.Closed ?? false);
        }).ToList();
    }

    public static IReadOnlyList<CropVisual> Crops(ObservableContext ctx, Func<CellCoord, bool> inScope) =>
        ctx.World.CropBatches.Where(crop => inScope(crop.Plot)).OrderBy(crop => crop.Id.Value)
            .Select(crop => Crop(ctx, crop)).ToList();

    public static CropVisual Crop(ObservableContext ctx, CropBatch crop)
    {
        var world = ctx.World;
        var farm = ctx.FarmAt(crop.Plot);
        var rules = world.AgricultureRules;
        long tick = world.CurrentDate.TotalHours;
        double fraction = crop.Status switch
        {
            CropStatus.Mature => 1,
            CropStatus.Harvested or CropStatus.Fallow => 0,
            _ when crop.MatureAtTick == long.MaxValue => rules.DevelopmentToMaturity > 0
                ? Math.Clamp(crop.Development / rules.DevelopmentToMaturity, 0, 1) : 0,
            _ => Math.Clamp(1.0 - Math.Max(0, crop.MatureAtTick - tick) / (double)Math.Max(1, crop.MatureAtTick - crop.PlantedAtTick), 0, 1),
        };
        fraction = Math.Round(fraction, 2);
        string stage = crop.Status switch
        {
            CropStatus.Harvested => "harvested",
            CropStatus.Fallow => "fallow",
            CropStatus.Mature => "mature",
            _ when rules.Enabled && crop.MatureAtTick == long.MaxValue && crop.LastSeedSown == 0 => "unsown",
            _ when fraction < 0.15 => "planted",
            _ => "growing",
        };
        double perHectare = rules.SeedPerHectare > 0 ? rules.SeedPerHectare : 1;
        return new CropVisual(
            crop.Id.Value, crop.Plot, crop.CropResourceId, crop.Status.ToString(), stage, fraction,
            crop.WaterDelivered < crop.WaterRequired,
            Math.Round(crop.LastSeedSown / perHectare, 2), Math.Round(crop.ArableSeed / perHectare, 2), crop.Standing,
            ctx.Names.Resource(crop.CropResourceId), farm?.Id.Value, farm?.Owner?.Value, ExpectedHarvest(world, crop, tick));
    }

    private static long ExpectedHarvest(WorldState world, CropBatch crop, long tick)
    {
        if (crop.Status == CropStatus.Mature) return crop.Standing;
        if (crop.Status is CropStatus.Harvested or CropStatus.Fallow) return 0;
        double climate = world.ClimateFactorAt(crop.Plot, tick) / 1000.0;
        return (long)Math.Floor(crop.EffectiveSeedMilli / 1000.0 * world.AgricultureRules.YieldPerSeed * climate);
    }

    public static IReadOnlyList<AnimalVisual> Animals(ObservableContext ctx, Func<CellCoord, bool> inScope)
    {
        var predators = ctx.World.AnimalSpeciesRules.GroupBy(r => r.Species).ToDictionary(g => g.Key, g => g.First().PredatorOf);
        return ctx.World.Fauna.Where(a => a.IsAlive && inScope(a.Position)).OrderBy(a => a.Id.Value)
            .Select(a => Animal(a, ctx.Tick, predators.GetValueOrDefault(a.Species))).ToList();
    }

    public static AnimalVisual Animal(LivingWorld.Domain.Fauna.Animal a, long tick, string? predatorOf) =>
        new(a.Id.Value, a.Species, a.Position, (int)(Math.Round(a.Energy.ValueAt(tick) / 10) * 10), a.VectorDisease is not null, predatorOf);

    public static IReadOnlyList<FaunaGroupVisual> FaunaGroups(WorldState world)
    {
        var groups = new SortedDictionary<(string, int), (int Count, long X, long Y)>();
        foreach (var animal in world.Fauna)
        {
            if (!animal.IsAlive || !world.Map.TryGetCell(animal.Position, out _)) continue;
            var key = (animal.Species, world.Map.RegionOf(animal.Position).Value);
            var g = groups.GetValueOrDefault(key);
            groups[key] = (g.Count + 1, g.X + animal.Position.X, g.Y + animal.Position.Y);
        }

        return groups.Select(g => new FaunaGroupVisual(g.Key.Item1, g.Key.Item2, g.Value.Count,
            new CellCoord((int)(g.Value.X / g.Value.Count), (int)(g.Value.Y / g.Value.Count))))
            .OrderBy(LivingWorld.Api.Visual.Scope.ScopeDeltaBuilder.FaunaKey).ToList();
    }

    public static IReadOnlyList<LeavingVisual> Leavings(ObservableContext ctx, Func<CellCoord, bool> inScope) =>
        ctx.World.Invention?.Leavings is { } leavings
            ? leavings.Where(l => inScope(new CellCoord(l.X, l.Y))).Select(l => Leaving(ctx, l)).ToList()
            : [];

    public static LeavingVisual Leaving(ObservableContext ctx, Leaving l)
    {
        long units = ctx.World.Invention?.LeftLoads?.Where(x => x.LeavingId == l.Id).Sum(x => (long)x.Quantity) ?? 0;
        return new LeavingVisual(
            l.Id, new CellCoord(l.X, l.Y), l.DeadId, l.HouseholdId >= 0 ? l.HouseholdId : null,
            l.HeirHouseholdId >= 0 ? l.HeirHouseholdId : null, l.Tick, units,
            l.ItemId != 0 ? l.ItemId : null, l.ItemId != 0 ? ctx.Item(l.ItemId)?.DesignId : null,
            l.FetcherId >= 0 ? l.FetcherId : null, l.CarrierId >= 0 ? l.CarrierId : null);
    }

    /// <summary>Peças inventadas da cidade em uso ou posse (não vencidas).</summary>
    public static IReadOnlyList<ObjectVisual> Objects(ObservableContext ctx, CityId city) =>
        ctx.Items.Where(item => item.City == city && item.ExpiresTick > ctx.Tick).OrderBy(item => item.Id)
            .Select(item => Object(ctx, item)).ToList();

    public static ObjectVisual Object(ObservableContext ctx, CraftedItem item)
    {
        var world = ctx.World;
        long? holder = ctx.HolderOf(item.Id);
        CellCoord? location = holder is { } h && world.FindNpc(new NpcId(h)) is { } npc ? npc.CurrentLocation
            : item.MarketId is { } market && world.FindWorkplace(new WorkplaceId(market)) is { } shop ? shop.Location
            : world.FindHousehold(new HouseholdId(item.HouseholdId))?.Location;
        var root = ctx.DesignOf(item.DesignId) is { Parts.Count: > 0 } design ? design.Parts[0] : (DesignPart?)null;
        return new ObjectVisual(
            item.Id, item.DesignId, item.HouseholdId, holder, location, item.ForSale, item.MarketId,
            root?.Shape.ToString().ToLowerInvariant(), root?.Material, Math.Round(item.QualityPm / 1000.0, 2));
    }

    /// <summary>Estação e clima numa célula (centro do escopo).</summary>
    public static ScopeClimateVisual Climate(WorldState world, CellCoord cell)
    {
        var date = world.CurrentDate;
        int monthsPerSeason = Math.Max(1, world.Calendar.MonthsPerYear / 4);
        int season = date.Month / monthsPerSeason;
        int region = world.Map.TryGetCell(cell, out _) ? world.Map.RegionOf(cell).Value : -1;
        int factor = world.ClimateYears.LastOrDefault(y => y.Region.Value == region && y.Year == date.Year)?.FactorMilli ?? 1000;
        double temperature = world.Map.TryGetCell(cell, out _)
            ? Half(EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, date.TotalHours))
            : 0;
        return new ScopeClimateVisual(
            date.Month, season, season < SeasonKeys.Length ? SeasonKeys[season] : $"season-{season}", temperature, region, factor,
            world.ClimateRules.Enabled && factor < ClimateRules.DroughtBelowMilli);
    }

    /// <summary>Camada de clima do mundo: estação e uma linha por região (célula representativa = a menor em X, depois Y, a mesma
    /// que o sistema de estação usa).</summary>
    public static ClimateLayerPayload ClimateLayer(WorldState world)
    {
        var global = Climate(world, new CellCoord(world.Map.Width / 2, world.Map.Height / 2));
        long tick = world.CurrentDate.TotalHours;
        long year = world.CurrentDate.Year;
        var regions = world.Map.Regions.Where(r => r.Cells.Count > 0).OrderBy(r => r.Id.Value).Select(region =>
        {
            var center = region.Cells.OrderBy(c => c.X).ThenBy(c => c.Y).First();
            int factor = world.ClimateYears.LastOrDefault(y => y.Region == region.Id && y.Year == year)?.FactorMilli ?? 1000;
            return new RegionClimateVisual(region.Id.Value, center,
                Half(EnvironmentTemperatureMechanic.EffectiveTemperature(world, center, tick)), factor,
                world.ClimateRules.Enabled && factor < ClimateRules.DroughtBelowMilli);
        }).ToArray();
        return new ClimateLayerPayload(global.Month, global.SeasonIndex, global.SeasonKey, new ValueList<RegionClimateVisual>(regions));
    }

    private static double Tenth(double value) => Math.Round(value * 10) / 10;
    private static double Half(double value) => Math.Round(value * 2) / 2;

    /// <summary>Estoque resumido: exato até 10, depois dois algarismos significativos (o delta não pisca a cada unidade).</summary>
    public static long Bucket(long value)
    {
        if (value <= 10) return value;
        long scale = 1;
        while (value / scale >= 100) scale *= 10;
        return value / scale * scale;
    }
}
