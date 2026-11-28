using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Skills;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Behavior.Needs;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Cities.Construction;
using LivingWorld.Simulation.Cities.Layout;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Lifecycle;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Population;

/// <summary>Gera a população inicial e registra em <see cref="WorldState"/>: chama o
/// gerador puro do Domain, adiciona NPCs/households e agenda a morte de cada um — nenhum deles
/// nasce sem um evento de óbito já na fila.</summary>
public static class PopulationSeeder
{
    internal const int InitialHouseBuildingTypeId = -1;

    public static void SeedInitial(WorldState world, int count, CultureId culture, CellCoord villageLocation, CityId city = default)
    {
        var placementCity = ResolvePlacementCity(world, city, villageLocation, culture);

        var rng = world.Rng.Stream("population-init");
        var houseIds = new List<BuildingId>();
        IReadOnlyList<IReadOnlyList<Npc>>? composition = null;
        var generated = PopulationGenerator.GenerateInitial(
            rng, world.CurrentDate, count, culture, villageLocation, world.PopulationRules.LifeTable,
            world.PopulationCatalog, world.NextNpcId, world.NextHouseholdId, placementCity.Id,
            householdLocationsFactory: householdCount => PlaceHouseholdBuildings(
                world, placementCity, count, householdCount, houseIds, composition),
            bodyRules: world.BodyRules, appearanceRules: world.AppearanceRules,
            householdsComposed: groups =>
            {
                composition = groups;
                Naming.PersonNaming.NameFounders(world, culture, groups); // AD-178: nome próprio e de família por casa
            });

        Register(world, generated, houseIds);
    }

    /// <summary>Famílias autoradas: mesma colocação de casa e mesma agenda de óbito/despertar da população
    /// sorteada. Cada família mora na cidade do seu assentamento (ou na da vila inicial).</summary>
    public static void SeedAuthored(
        WorldState world, AuthoredHousehold household, CultureId culture, CellCoord villageLocation, City city)
    {
        var rng = world.Rng.Stream("population-authored");
        var houseIds = new List<BuildingId>();
        IReadOnlyList<IReadOnlyList<Npc>>? composition = null;
        var generated = PopulationGenerator.GenerateAuthored(
            rng, world.CurrentDate, [household], culture, villageLocation, world.NextNpcId, world.NextHouseholdId, city.Id,
            householdLocationsFactory: householdCount => PlaceHouseholdBuildings(
                world, city, household.Members.Count, householdCount, houseIds, composition),
            bodyRules: world.BodyRules, appearanceRules: world.AppearanceRules,
            householdsComposed: groups => composition = groups);
        Register(world, generated, houseIds);
    }

    private static void Register(WorldState world, PopulationGenerator.GeneratedPopulation generated, List<BuildingId> houseIds)
    {
        foreach (var npc in generated.Npcs)
        {
            npc.ConfigureNeedDecay(world.NeedsRules, world.CurrentDate.TotalHours);
            LivingWorld.Simulation.Objects.HomeLife.Seed(world, npc); // #255: necessidades do catálogo do cenário
            LivingWorld.Simulation.Population.Body.BodyGrowth.InitializeFounder(world, npc); // corpo da idade (AD-134)
            world.AddNpc(npc);
            LivingWorld.Simulation.Ontogeny.OntogenyMechanic.Initialize(world, npc);
            SeedPracticedSkill(world, npc);
        }
        for (int i = 0; i < generated.Households.Count; i++)
        {
            // Mesma ordem de householdLocationsFactory: a casa i pertence ao household i.
            generated.Households[i].LiveIn(houseIds[i]);
            world.AddHousehold(generated.Households[i]);
        }

        foreach (var npc in generated.Npcs) LivingWorld.Simulation.Objects.SocialInteractions.SeedBond(world, npc); // #259: o casal começa com o afeto do catálogo social
        world.AdvanceNpcIdTo(generated.NextNpcId);
        world.AdvanceHouseholdIdTo(generated.NextHouseholdId);

        var ctx = new TickContext(world, world.Rng, world.Scheduler);
        foreach (var npc in generated.Npcs)
        {
            MortalitySystem.CoverUntilNextSweep(world, ctx, npc);
            NpcWakeScheduler.ScheduleWake(world, ctx, npc.Id.Value, world.CurrentDate.TotalHours + 1);
        }
    }

    /// <summary>Quem já existe quando o mundo começa não teve a infância nem os anos de ofício simulados (AD-132): recebe, no
    /// ofício da própria profissão, a habilidade esperada de quem o praticou desde criança sob as regras de aprendizado do
    /// cenário (<see cref="SkillLearningRules.ExpectedPracticedSkill"/>). Habilidade autorada vence.</summary>
    internal static void SeedPracticedSkill(WorldState world, Npc npc)
    {
        var rules = LivingWorld.Simulation.Scenarios.ScenarioRunner.DefaultSkillsRules;
        if (npc.Skills.Values.Count > 0 || !rules.SkillByProfession.TryGetValue(npc.Profession.Id, out var skill)) return;
        var calendar = world.CurrentDate.Calendar;
        double age = (double)(world.CurrentDate.TotalHours - npc.BirthDate.TotalHours) / calendar.HoursPerYear;
        double level = rules.Learning.ExpectedPracticedSkill(
            age, rules.BaseRateBySource.GetValueOrDefault(SkillGainSource.Practice), rules.Cap, calendar.DaysPerMonth, calendar.MonthsPerYear);
        if (level > 0) npc.GainSkill(skill, level, rules.Cap);
    }

    private static City ResolvePlacementCity(WorldState world, CityId requestedCity, CellCoord villageLocation, CultureId culture)
    {
        if (requestedCity != default)
            return world.FindActiveCity(requestedCity)
                ?? throw new InvalidOperationException("Population city must exist before household placement.");

        var existing = world.ActiveCities().FirstOrDefault(candidate => candidate.Location == villageLocation);
        if (existing is not null) return existing;

        var created = new City(
            world.NextCityId(), villageLocation, world.CurrentDate.TotalHours,
            foundedFromCityId: null, AggregatePopulationPool.Empty, name: CityNameGenerator.Generate(world, culture)); // AD-178
        world.AddCity(created);
        return created;
    }

    // Post-ship fix (bug real "casas gigantes, ultrapassam o limite da cidade"): usava um scan
    // dedicado (ResolveInitialBatch) que caía num fallback varrendo o MAPA INTEIRO sempre que uma
    // casa não cabia nos bounds ainda pequenos (population-derived) de uma cidade recém-fundada —
    // espalhando casas arbitrariamente longe da cidade, fora do alcance de
    // CityOccupancy.ResolveGrownBounds's AbsorptionRingCells (que só absorve overflow PRÓXIMO da
    // borda). Reusa exatamente o mesmo caminho que todo outro prédio já usa
    // (BuildingPlacementResolver.Resolve: célula livre nos bounds, senão anel de overflow a partir
    // da borda) — mesmo padrão de peek-id/adicionar-incremental de ConstructionSystem.CompleteProject,
    // pra que Resolve veja as casas já colocadas neste lote como ocupadas.
    private static IReadOnlyList<CellCoord> PlaceHouseholdBuildings(
        WorldState world, City city, int population, int householdCount, List<BuildingId> houseIds,
        IReadOnlyList<IReadOnlyList<Npc>>? composition = null)
    {
        var locations = new List<CellCoord>(householdCount);
        for (int i = 0; i < householdCount; i++)
        {
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
            var shape = world.ObjectCatalog?.Rooms?.ShapeFor(InitialHouseBuildingTypeId); // o tamanho da casa vem do cenário (Rooms.shapes)
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, InitialHouseBuildingTypeId, world.CurrentDate.TotalHours, shape: shape);
            // Post-ship fix (real-household-workplace-buildings, map-auto-resize removal): a
            // small authored map can run out of free rectangular room for every household before
            // this loop is done -- now common, since the map is never silently regenerated bigger
            // to make room anymore (that was the actual bug: same seed, different world). Same
            // last-resort ring/hash position BuildingPlacementResolver.ResolveQueuedSite already
            // uses for a building with nowhere else to go (never fails, may overlap): a household
            // still needs SOME location to exist (unlike a workplace, it can't just be skipped),
            // and land scarcity should degrade its exact position, never crash world loading.
            // AD-173: a família escolhe o lote pelo ofício, pelo tamanho e pelos meios; primeiro de frente para a rua
            // (degraus da rua), depois um andar livre de casa que já existe, só então os degraus sem rua.
            var profile = composition is not null && i < composition.Count
                ? LotProfiles.ForHousehold(world, city.Id, composition[i])
                : LotProfile.UnknownHousehold;
            // Sem andar livre nem terra seca com porta, a família divide a casa menos cheia (aperto) antes de erguer casa sobre a
            // água ou sem saída (último degrau). Traçado desligado: a ordem de antes (todos os degraus, depois o andar livre).
            bool layout = world.CityRules.EffectiveLayout().Enabled;
            int lastBeforeFloors = layout ? PlacementPolicy.StreetLevels - 1 : PlacementPolicy.LevelCount - 1;
            var placement = BuildingPlacementResolver.ResolveLot(
                candidate, city, world, bounds, profile, 0, lastBeforeFloors, chooseOrientation: true);
            var shared = placement is null ? FindDwellingWithFreeFloor(world, city, houseIds) : null;
            if (layout && placement is null && shared is null)
                placement = BuildingPlacementResolver.ResolveLot(
                    candidate, city, world, bounds, profile, PlacementPolicy.StreetLevels, PlacementPolicy.LevelCount - 2, chooseOrientation: true);
            if (layout && placement is null && shared is null)
                shared = LeastCrowdedDwelling(world, city, houseIds);
            if (layout && placement is null && shared is null)
                placement = BuildingPlacementResolver.ResolveLot(
                    candidate, city, world, bounds, profile, PlacementPolicy.LevelCount - 1, PlacementPolicy.LevelCount - 1, chooseOrientation: true);

            // city-building-separation: sem terra livre a casa não vira um prédio empilhado sobre outro;
            // ela se estende para cima — o domicílio passa a morar num andar livre de uma casa que já existe
            // (uma casa de N andares abriga até N domicílios).
            if (placement is null && shared is not null)
            {
                houseIds.Add(shared.Value.Building.Id);
                locations.Add(DwellingCapacity.InteriorCellOf(shared.Value.Building, shared.Value.Building.Position!.Value));
                continue;
            }

            var (position, orientation, croft) = placement is { } resolved
                ? (resolved.Position, resolved.Orientation, resolved.Croft)
                : (ClampFootprintIntoMap(world, candidate, CityOccupancy.LegacyRingFallback(candidate.Id, city.Location)), 0, 0);

            var house = new Building(
                world.NextBuildingIdAndAdvance(), city.Id, InitialHouseBuildingTypeId,
                world.CurrentDate.TotalHours, position, orientation, croftCells: croft, shape: shape);
            world.AddBuilding(house);
            houseIds.Add(house.Id);
            locations.Add(DwellingCapacity.InteriorCellOf(house, position));
        }
        return locations;
    }

    /// <summary>Último recurso da semeadura: o anel legado ignora o mapa, então a origem é puxada para que o
    /// footprint inteiro fique dentro dele (nada nasce fora do mundo, mesmo quando falta terra).</summary>
    private static CellCoord ClampFootprintIntoMap(WorldState world, Building house, CellCoord origin)
    {
        var shape = BuildingFootprintGenerator.Generate(house.Id, house.BuildingTypeId, orientation: 0, house.Shape);
        int width = shape.Max(c => c.Cell.X) + 1, height = shape.Max(c => c.Cell.Y) + 1;
        return new CellCoord(
            Math.Clamp(origin.X, 0, Math.Max(0, world.Map.Width - width)),
            Math.Clamp(origin.Y, 0, Math.Max(0, world.Map.Height - height)));
    }

    /// <summary>Primeira casa da cidade (por Id) com andar sobrando: moradores atuais, mais os domicílios já
    /// atribuídos neste lote, abaixo do número de andares. Só considera casas com posição gravada.</summary>
    /// <summary>AD-173: a casa (com posição gravada) que menos domicílios abriga agora, contando o lote; desempate por Id.</summary>
    private static (Building Building, int Tenants)? LeastCrowdedDwelling(WorldState world, City city, List<BuildingId> batchHouseIds)
    {
        (Building Building, int Tenants)? best = null;
        foreach (var house in world.Buildings
                     .Where(b => b.City == city.Id && b.BuildingTypeId == InitialHouseBuildingTypeId && b.Position is not null)
                     .OrderBy(b => b.Id.Value))
        {
            int tenants = world.Households.Count(h => h.Dwelling == house.Id) + batchHouseIds.Count(id => id == house.Id);
            if (best is null || tenants < best.Value.Tenants) best = (house, tenants);
        }
        return best;
    }

    private static (Building Building, int Tenants)? FindDwellingWithFreeFloor(WorldState world, City city, List<BuildingId> batchHouseIds)
    {
        foreach (var house in world.Buildings
                     .Where(b => b.City == city.Id && b.BuildingTypeId == InitialHouseBuildingTypeId && b.Position is not null)
                     .OrderBy(b => b.Id.Value))
        {
            int floors = DwellingCapacity.Of(world, house);
            int tenants = world.Households.Count(h => h.Dwelling == house.Id) + batchHouseIds.Count(id => id == house.Id);
            if (tenants < floors) return (house, tenants);
        }
        return null;
    }
}
