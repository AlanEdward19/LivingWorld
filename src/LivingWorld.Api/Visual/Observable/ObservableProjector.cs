using System.Collections.Concurrent;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Health;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
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
    private static readonly ConcurrentDictionary<(long, int, int, BuildingShape?), BuildingPlan> Plans = new();

    public static NpcObservable Npc(ObservableContext ctx, Npc npc) => Npc(ctx, npc, null);

    /// <summary>AD-172: com o observável do tick anterior, devolve o mesmo registro quando nada visível mudou (e reaproveita as
    /// partes que não mudaram): sem alocar por NPC parado e o diff compara por referência.</summary>
    public static NpcObservable Npc(ObservableContext ctx, Npc npc, NpcObservable? previous)
    {
        var world = ctx.World;
        int age = npc.AgeYears(world.CurrentDate);
        var affect = PsycheMechanic.AffectOf(world, npc, ctx.Tick);
        double grief = Tenth(affect.Grief), fear = Tenth(affect.Fear), anger = Tenth(affect.Anger), joy = Tenth(affect.Joy),
            shame = Tenth(affect.Shame), loneliness = Tenth(affect.Loneliness);
        var emotions = previous?.Emotions is { } e && e.Grief == grief && e.Fear == fear && e.Anger == anger && e.Joy == joy
            && e.Shame == shame && e.Loneliness == loneliness
            ? e
            : new EmotionsVisual(grief, fear, anger, joy, shame, loneliness);
        var carried = CarriedOf(ctx, npc, previous?.Carried);
        HeldObjectVisual? held = ctx.HeldOf(npc.Id.Value) is { } itemId
            ? previous?.Held is { } h && h.ItemId == itemId && h.DesignId == (ctx.Item(itemId)?.DesignId ?? 0)
                ? h
                : new HeldObjectVisual(itemId, ctx.Item(itemId)?.DesignId ?? 0)
            : null;
        IllnessVisual? illness = ctx.InfectionOf(npc.Id.Value) is { } infection
            ? previous?.Illness is { } i && i.DiseaseId == infection.Disease && i.Symptomatic == infection.Symptomatic
                && i.Bedridden == infection.Bedridden
                ? i
                : new IllnessVisual(infection.Disease, infection.Symptomatic, infection.Bedridden)
            : null;
        var pregnancy = PregnancyOf(world, npc, ctx.Tick, previous?.Pregnancy);
        double growth = Math.Round(npc.AdultHeight > 0 ? Math.Clamp(npc.Height / npc.AdultHeight, 0, 1) : 1, 2);
        int heightCm = (int)Math.Round(npc.Height * 100);
        string sex = npc.Sex.ToString();
        string lifeStage = world.LifeStageRules.LifeStageOf(age).ToString();
        string? profession = ctx.Names.Profession(npc.Profession.Id);
        bool mourning = PsycheMechanic.Mourns(affect); // AD-183: o critério do motor, não um limiar de tela
        string? combat = CombatOf(world, npc);
        if (previous is not null && previous.Mourning == mourning && previous.Combat == combat && ReferenceEquals(previous.Emotions, emotions) && ReferenceEquals(previous.Carried, carried)
            && ReferenceEquals(previous.Held, held) && ReferenceEquals(previous.Illness, illness)
            && ReferenceEquals(previous.Pregnancy, pregnancy) && previous.AgeYears == age && previous.HeightCm == heightCm
            && previous.GrowthFraction == growth && previous.ProfessionId == npc.Profession.Id && previous.Name == npc.Name
            && previous.Sex == sex && previous.LifeStage == lifeStage && previous.ProfessionName == profession)
            return previous;
        return new NpcObservable(
            npc.Name, sex, age, lifeStage, npc.Profession.Id, profession, heightCm, growth,
            pregnancy, illness, emotions, Dominant(emotions), mourning, carried, held, combat);
    }

    /// <summary>#234: a reação de briga ainda em curso (até o instante marcado pelo motor); só leitura, nula sem briga.</summary>
    private static string? CombatOf(WorldState world, Npc npc) =>
        npc.Combat is { Reaction: not CombatReaction.None } c && world.CurrentDate.TotalMilliseconds <= c.ReactionUntilMs
            ? c.Reaction switch
            {
                CombatReaction.Retaliate => "retaliate",
                CombatReaction.Flee => "flee",
                CombatReaction.CallHelp => "callHelp",
                _ => "recoil",
            }
            : null;

    private static IReadOnlyList<CarriedVisual> CarriedOf(ObservableContext ctx, Npc npc, IReadOnlyList<CarriedVisual>? previous)
    {
        var loads = ctx.CarriedOf(npc.Id.Value);
        // AD-195: o que leva nas mãos (o material da obra a caminho do lote) também é carga visível
        if (npc.IsCarrying)
        {
            var hands = new CarriedVisual(npc.CarriedResourceId, (int)Math.Min(int.MaxValue, npc.CarriedQuantity), ctx.Names.Resource(npc.CarriedResourceId));
            var all = new CarriedVisual[loads.Count + 1];
            for (int i = 0; i < loads.Count; i++) all[i] = new CarriedVisual(loads[i].ResourceId, loads[i].Quantity, ctx.Names.Resource(loads[i].ResourceId));
            all[^1] = hands;
            var fresh = new ValueList<CarriedVisual>(all);
            return previous is ValueList<CarriedVisual> before && before.Equals(fresh) ? previous : fresh;
        }
        if (loads.Count == 0) return ValueList<CarriedVisual>.Empty;
        if (previous is { } old && old.Count == loads.Count)
        {
            bool same = true;
            for (int i = 0; i < loads.Count && same; i++)
                same = old[i].ResourceId == loads[i].ResourceId && old[i].Quantity == loads[i].Quantity
                    && old[i].ResourceName == ctx.Names.Resource(loads[i].ResourceId);
            if (same) return old;
        }

        var items = new CarriedVisual[loads.Count];
        for (int i = 0; i < items.Length; i++)
            items[i] = new CarriedVisual(loads[i].ResourceId, loads[i].Quantity, ctx.Names.Resource(loads[i].ResourceId));
        return new ValueList<CarriedVisual>(items);
    }

    private static PregnancyVisual? PregnancyOf(WorldState world, Npc npc, long tick, PregnancyVisual? previous)
    {
        if (npc.Reproduction?.Pregnancy is not { } pregnancy) return null;
        int hpd = world.Calendar.HoursPerDay;
        long shows = pregnancy.ConceivedTick + (long)world.FamilyRules.Unions.Intimacy.ShowsAfterDays * hpd;
        if (tick < shows || tick >= pregnancy.EndTick) return null;
        double fraction = (tick - pregnancy.ConceivedTick) / (double)Math.Max(1L, (long)pregnancy.GestationDays * hpd);
        double shown = Math.Round(Math.Clamp(fraction, 0, 1) * 20) / 20;
        int babies = Math.Max(1, pregnancy.Sexes.Length);
        return previous is { } p && p.Fraction == shown && p.Babies == babies ? p : new PregnancyVisual(shown, babies);
    }

    /// <summary>A emoção mais forte (a primeira em caso de empate, na ordem do contrato), só a partir de um décimo.</summary>
    private static string? Dominant(EmotionsVisual e)
    {
        string name = "grief";
        double top = e.Grief;
        if (e.Fear > top) (name, top) = ("fear", e.Fear);
        if (e.Anger > top) (name, top) = ("anger", e.Anger);
        if (e.Joy > top) (name, top) = ("joy", e.Joy);
        if (e.Shame > top) (name, top) = ("shame", e.Shame);
        if (e.Loneliness > top) (name, top) = ("loneliness", e.Loneliness);
        return top >= 0.1 ? name : null;
    }

    /// <summary>Densidade por cidade para o escopo Mundo, numa passada pelos NPCs já situados (AD-172: a mesma leitura do tick).</summary>
    public static Dictionary<CityId, CityDensityVisual> Densities(WorldState world, IReadOnlyList<LivingWorld.Api.Visual.Scope.ScopedNpc> npcs)
    {
        var inside = new Dictionary<CityId, (int Count, Dictionary<int, int> Actions)>();
        var outdoors = new Dictionary<CityId, int>();
        var footprints = BuildingFootprints.Of(world);
        foreach (var scoped in npcs)
        {
            if (!scoped.HasCityBounds || !scoped.InsideCity) continue;
            var npc = scoped.Npc;
            if (!footprints.Contains(npc.CurrentLocation)) outdoors[npc.City] = outdoors.GetValueOrDefault(npc.City) + 1;
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
                Half(EnvironmentTemperatureMechanic.EffectiveTemperature(world, city.Location, world.CurrentDate.TotalHours)),
                outdoors.GetValueOrDefault(city.Id));
        }

        return result;
    }

    public static BuildingPlan Plan(Building building, int orientation)
    {
        if (Plans.Count > 50_000) Plans.Clear();
        return Plans.GetOrAdd((building.Id.Value, building.BuildingTypeId, orientation, building.Shape), key =>
        {
            var layout = BuildingLayoutGenerator.Generate(new BuildingId(key.Item1), key.Item2, key.Item3, key.Item4);
            var ground = layout.FloorLayouts[0];
            return new BuildingPlan(layout.Floors, new ValueList<FootprintCell>(ground.Cells.ToArray()),
                new ValueList<LivingWorld.Domain.Cities.Buildings.WallSegment>((ground.Walls ?? []).ToArray()),
                new ValueList<LivingWorld.Domain.Cities.Buildings.WallSegment>((ground.Doors ?? []).ToArray()));
        });
    }

    /// <summary>Função, dono, moradores, trabalhadores e estoque resumido dos prédios visíveis de uma cidade. AD-172: uma passada pelas
    /// casas e oficinas, sem LINQ por prédio; com o registro do tick anterior (<paramref name="previousOf"/>), o prédio que não mudou
    /// devolve o mesmo registro e as listas iguais são reaproveitadas.</summary>
    public static IReadOnlyList<BuildingStateVisual> BuildingStates(
        ObservableContext ctx, IReadOnlyList<Building> visible, Func<long, BuildingStateVisual?>? previousOf = null)
    {
        if (visible.Count == 0) return [];
        var world = ctx.World;
        var homesOf = new Dictionary<BuildingId, List<Household>>(visible.Count);
        var workplaceOf = new Dictionary<BuildingId, Workplace?>(visible.Count);
        foreach (var building in visible)
        {
            homesOf.TryAdd(building.Id, null!);
            workplaceOf.TryAdd(building.Id, null);
        }

        foreach (var household in world.Households)
            if (household.Dwelling is { } dwelling && homesOf.TryGetValue(dwelling, out var list))
                (list ?? (homesOf[dwelling] = new List<Household>(2))).Add(household);
        foreach (var workplace in world.Workplaces)
            if (workplace.Building is { } at && workplaceOf.TryGetValue(at, out var current)
                && (current is null || workplace.Id.Value < current.Id.Value))
                workplaceOf[at] = workplace;

        var recipes = world.CityCatalog.BuildingRecipes;
        var result = new BuildingStateVisual[visible.Count];
        bool night = IsNight(world);
        var householdIds = new List<long>();
        var residents = new List<long>();
        var workers = new List<long>();
        var stock = new List<KeyValuePair<ResourceType, long>>(6);
        for (int index = 0; index < visible.Count; index++)
        {
            var building = visible[index];
            var previous = previousOf?.Invoke(building.Id.Value);
            var homes = homesOf[building.Id];
            if (homes is { Count: > 1 }) homes.Sort((x, y) => x.Id.Value.CompareTo(y.Id.Value));
            int homeCount = homes?.Count ?? 0;
            var workplace = workplaceOf[building.Id];
            recipes.TryGetValue(building.BuildingTypeId, out var recipe);
            string? living = recipe?.Lodging is not null ? "lodging"
                : homeCount > 0 || recipe?.HousingCapacityProvided > 0 ? "dwelling"
                : null;
            string function = (living, workplace is not null) switch
            {
                ("lodging", true) => "lodging,workplace",
                ("dwelling", true) => "dwelling,workplace",
                (null, true) => "workplace",
                ({ } only, false) => only,
                _ => "structure",
            };

            householdIds.Clear();
            residents.Clear();
            workers.Clear();
            long pantry = 0;
            for (int i = 0; i < homeCount; i++)
            {
                householdIds.Add(homes![i].Id.Value);
                foreach (var member in homes[i].Members) residents.Add(member.Value);
                foreach (var units in homes[i].Stock.Values) pantry += units;
            }

            residents.Sort();
            if (workplace is not null)
                foreach (var employee in workplace.Employees) workers.Add(employee.Value);
            workers.Sort();
            TopStock(workplace, stock);

            var households = Reuse(previous?.HouseholdIds, householdIds);
            var people = Reuse(previous?.ResidentIds, residents);
            var staff = Reuse(previous?.WorkerIds, workers);
            var lines = ReuseStock(ctx, previous?.Stock, stock);
            string? typeName = ctx.Names.BuildingType(building.BuildingTypeId);
            string? locationName = workplace is null ? null : ctx.Names.LocationType(workplace.LocationType.Id);
            bool closed = workplace?.Closed ?? false;
            bool lit = night && SomeoneAwakeInside(world, homes, homeCount, workplace);
            result[index] = previous is not null && ReferenceEquals(previous.HouseholdIds, households)
                && ReferenceEquals(previous.ResidentIds, people) && ReferenceEquals(previous.WorkerIds, staff)
                && ReferenceEquals(previous.Stock, lines) && previous.BuildingId == building.Id.Value
                && previous.BuildingTypeId == building.BuildingTypeId && previous.TypeName == typeName && previous.Function == function
                && previous.WorkplaceId == workplace?.Id.Value && previous.LocationTypeId == workplace?.LocationType.Id
                && previous.LocationTypeName == locationName && previous.OwnerId == workplace?.Owner?.Value
                && previous.PantryUnits == pantry && previous.Closed == closed && previous.Lit == lit
                ? previous
                : new BuildingStateVisual(
                    building.Id.Value, building.BuildingTypeId, typeName, function,
                    workplace?.Id.Value, workplace?.LocationType.Id, locationName, workplace?.Owner?.Value,
                    households, people, staff, lines, pantry, closed, lit);
        }

        return result;
    }

    /// <summary>AD-189: depois do pôr e antes do nascer do sol do dia (hora solar, a luz do tempo do motor); sem o tempo, nunca.</summary>
    public static bool IsNight(WorldState world) => LivingWorld.Simulation.Objects.HomeTending.IsDark(world); // #258: a mesma conta do NPC que acende a luz

    /// <summary>AD-189: alguém acordado dentro: morador vivo na célula da casa sem estar dormindo, ou trabalhador na oficina.</summary>
    private static bool SomeoneAwakeInside(WorldState world, List<Household>? homes, int homeCount, Workplace? workplace)
    {
        for (int i = 0; i < homeCount; i++)
            foreach (var member in homes![i].Members)
                if (world.FindNpc(member) is { IsAlive: true } npc && npc.CurrentLocation == homes[i].Location && npc.CurrentAction != ActionType.Sleep)
                    return true;
        if (workplace is not null)
            foreach (var employee in workplace.Employees)
                if (world.FindNpc(employee) is { IsAlive: true } npc && npc.CurrentLocation == workplace.Location && npc.CurrentAction != ActionType.Sleep)
                    return true;
        return false;
    }

    /// <summary>A lista anterior quando tem o mesmo conteúdo; senão uma nova (vazia compartilhada).</summary>
    private static IReadOnlyList<long> Reuse(IReadOnlyList<long>? previous, List<long> current)
    {
        if (previous is not null && previous.Count == current.Count)
        {
            bool same = true;
            for (int i = 0; i < current.Count && same; i++) same = previous[i] == current[i];
            if (same) return previous;
        }

        return current.Count == 0 ? ValueList<long>.Empty : new ValueList<long>(current.ToArray());
    }

    /// <summary>As cinco maiores linhas de estoque da oficina (maior quantidade, depois menor recurso).</summary>
    private static void TopStock(Workplace? workplace, List<KeyValuePair<ResourceType, long>> top)
    {
        top.Clear();
        if (workplace is null) return;
        foreach (var line in workplace.Stock)
        {
            if (line.Value <= 0) continue;
            int at = top.Count;
            while (at > 0 && (top[at - 1].Value < line.Value || (top[at - 1].Value == line.Value && top[at - 1].Key.Id > line.Key.Id))) at--;
            if (at >= 5) continue;
            top.Insert(at, line);
            if (top.Count > 5) top.RemoveAt(5);
        }
    }

    /// <summary>Estoque resumido; a lista anterior quando as linhas resumidas são as mesmas.</summary>
    private static IReadOnlyList<StockLine> ReuseStock(ObservableContext ctx, IReadOnlyList<StockLine>? previous, List<KeyValuePair<ResourceType, long>> top)
    {
        if (previous is not null && previous.Count == top.Count)
        {
            bool same = true;
            for (int i = 0; i < top.Count && same; i++)
                same = previous[i].ResourceId == top[i].Key.Id && previous[i].Quantity == Bucket(top[i].Value)
                    && previous[i].ResourceName == ctx.Names.Resource(top[i].Key.Id);
            if (same) return previous;
        }

        if (top.Count == 0) return ValueList<StockLine>.Empty;
        var lines = new StockLine[top.Count];
        for (int i = 0; i < lines.Length; i++)
            lines[i] = new StockLine(top[i].Key.Id, Bucket(top[i].Value), ctx.Names.Resource(top[i].Key.Id));
        return new ValueList<StockLine>(lines);
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
        // AD-172: dicionário comum (a ordem final vem só da chave espécie:região, única por bando); o ordenado comparava texto por
        // cultura a cada animal.
        var groups = new Dictionary<(string, int), (int Count, long X, long Y)>();
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
        var weather = world.Map.TryGetCell(cell, out _) ? Weather(world, cell) : null;
        return new ScopeClimateVisual(
            date.Month, season, season < SeasonKeys.Length ? SeasonKeys[season] : $"season-{season}", temperature, region, factor,
            weather?.Drought ?? (world.ClimateRules.Enabled && factor < ClimateRules.DroughtBelowMilli), weather);
    }

    /// <summary>AD-180: o tempo do dia na célula, lido do estado do motor (nada inventado aqui); nulo com o tempo desligado.</summary>
    public static WeatherVisual? Weather(WorldState world, CellCoord cell)
    {
        if (LivingWorld.Simulation.Climate.WeatherNow.RowAt(world, cell) is not { } row || world.Weather is not { } ledger) return null;
        var rules = world.ClimateRules.Weather!;
        double mm = row.PrecipTenths / 10.0;
        string precipitation = row.Kind switch
        {
            LivingWorld.Simulation.Climate.Precipitation.Rain => "rain",
            LivingWorld.Simulation.Climate.Precipitation.Snow => "snow",
            _ => "none",
        };
        string intensity = row.Kind == LivingWorld.Simulation.Climate.Precipitation.None ? "none" : mm < 2 ? "light" : mm < rules.HeavyRainMm ? "moderate" : "heavy";
        double now = LivingWorld.Simulation.Climate.WeatherNow.TemperatureAt(world, cell, world.CurrentDate.TotalHours);
        double daylight = ledger.DaylightMinutes / 60.0;
        string ground = LivingWorld.Simulation.Climate.WeatherNow.Ground(rules, row) switch
        {
            LivingWorld.Simulation.Climate.GroundWeather.Wet => "wet",
            LivingWorld.Simulation.Climate.GroundWeather.Mud => "mud",
            LivingWorld.Simulation.Climate.GroundWeather.HeavyMud => "heavy-mud",
            LivingWorld.Simulation.Climate.GroundWeather.Snow => "snow",
            LivingWorld.Simulation.Climate.GroundWeather.Frozen => "frozen",
            _ => "dry",
        };
        double daily = EnvironmentTemperatureMechanic.EffectiveTemperature(world, cell, world.CurrentDate.TotalHours);
        double cellShift = daily - row.MeanCenti / 100.0;
        var (hourly, hourlyFeels) = Hourly(world, row, daily);
        int hour = (int)(world.CurrentDate.TotalHours % world.Calendar.HoursPerDay);
        if (hour < hourly.Count) now = hourly[hour]; // a hora vem da mesma curva que o cliente lê
        return new WeatherVisual(
            precipitation, intensity, Tenth(mm), row.Storm, row.Hail, row.Frost,
            LivingWorld.Simulation.Climate.WeatherNow.InDrought(rules, row), row.HotDays >= rules.WaveDays, row.ColdDays >= rules.WaveDays,
            Tenth(row.WindDecims / 10.0), Half(now), hour < hourlyFeels.Count ? hourlyFeels[hour] : Half(LivingWorld.Simulation.Climate.WeatherNow.ApparentC(now, row)),
            Half(row.MinCenti / 100.0 + cellShift), Half(row.MaxCenti / 100.0 + cellShift),
            Tenth(daylight), Tenth(12 - daylight / 2), Tenth(12 + daylight / 2), ground, row.SnowMm, row.SoilPm, row.OutdoorLostDays,
            row.WindFromDeg, rules.FoliageEnabled ? FoliageOf(rules, row) : null, hourly, hourlyFeels);
    }

    /// <summary>AD-189: a folhagem da região (fração, fase e a cor do outono: o frio acumulado sobre o limiar da queda).</summary>
    private static FoliageVisual FoliageOf(LivingWorld.Domain.Climate.WeatherRules rules, LivingWorld.Simulation.Climate.WeatherRegionRow row)
    {
        string phase = row.LeafPhase switch
        {
            LivingWorld.Simulation.Climate.LeafPhase.Budding => "budding",
            LivingWorld.Simulation.Climate.LeafPhase.Full => "full",
            LivingWorld.Simulation.Climate.LeafPhase.Falling => "falling",
            _ => "bare",
        };
        double turning = row.LeafPhase == LivingWorld.Simulation.Climate.LeafPhase.Falling ? 1
            : Math.Clamp(row.LeafColdTenths / Math.Max(1, rules.LeafFallColdDegreeDays * 10), 0, 1);
        return new FoliageVisual(Math.Round(row.LeafPm / 1000.0, 2), phase, Math.Round(turning, 2));
    }

    [ThreadStatic]
    private static (WorldState? World, LivingWorld.Simulation.Climate.WeatherRegionRow? Row, long Version, double Daily,
        ValueList<double>? Hourly, ValueList<double>? Feels) _hourly;

    /// <summary>AD-189: a temperatura e a sensação de cada hora do dia (meio grau), pela mesma curva do motor
    /// (<c>WeatherNow.TemperatureAt</c>: mínima às 5 h, máxima às 15 h). Uma vez por dia e lugar; o resto do dia é o mesmo registro.</summary>
    private static (ValueList<double> Hourly, ValueList<double> Feels) Hourly(WorldState world, LivingWorld.Simulation.Climate.WeatherRegionRow row, double daily)
    {
        var known = _hourly;
        long version = world.Weather?.Version ?? -1;
        if (ReferenceEquals(known.World, world) && ReferenceEquals(known.Row, row) && known.Version == version && known.Daily == daily)
            return (known.Hourly!, known.Feels!);
        int hours = world.Calendar.HoursPerDay;
        var temps = new double[hours];
        var feels = new double[hours];
        for (int h = 0; h < hours; h++)
        {
            double t = daily + LivingWorld.Simulation.Climate.WeatherNow.DiurnalShape(h, hours) * (row.MaxCenti - row.MinCenti) / 200.0;
            temps[h] = Half(t);
            feels[h] = Half(LivingWorld.Simulation.Climate.WeatherNow.ApparentC(t, row));
        }
        var result = (new ValueList<double>(temps), new ValueList<double>(feels));
        _hourly = (world, row, version, daily, result.Item1, result.Item2);
        return result;
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
            var weather = Weather(world, center);
            return new RegionClimateVisual(region.Id.Value, center,
                Half(EnvironmentTemperatureMechanic.EffectiveTemperature(world, center, tick)), factor,
                weather?.Drought ?? (world.ClimateRules.Enabled && factor < ClimateRules.DroughtBelowMilli), weather);
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
