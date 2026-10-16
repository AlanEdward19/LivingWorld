using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Body;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Population;

public class PopulationGeneratorTests
{
    private static readonly LifeTable Table = LifeTable.Create(90,
    [
        new LifeTableBracket(0, 1, 0.08),
        new LifeTableBracket(2, 14, 0.01),
        new LifeTableBracket(15, 39, 0.004),
        new LifeTableBracket(40, 59, 0.01),
        new LifeTableBracket(60, 79, 0.04),
        new LifeTableBracket(80, 89, 0.15),
    ]).Value!;

    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly PopulationCatalog EmptyCatalog = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    [Fact]
    public void Initial_population_is_not_all_the_same_age_nor_all_adults()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200); // margem para não truncar crianças no ano 0
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(1), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        var ages = generated.Npcs.Select(n => n.AgeYears(now)).ToList();
        Assert.True(ages.Distinct().Count() > 5, "pirâmide etária não pode ser uma idade só repetida");
        Assert.Contains(ages, a => a < 18); // tem criança/jovem
        Assert.Contains(ages, a => a >= 18); // tem adulto
    }

    [Fact]
    public void Both_sexes_are_represented()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(2), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.Contains(generated.Npcs, n => n.Sex == Sex.Female);
        Assert.Contains(generated.Npcs, n => n.Sex == Sex.Male);
    }

    [Fact]
    public void Every_generated_npc_belongs_to_exactly_one_household_and_every_head_is_a_member()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(3), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        var membership = generated.Households.SelectMany(h => h.Members).ToList();
        Assert.Equal(generated.Npcs.Count, membership.Count); // sem duplicata, sem órfão
        Assert.Equal(membership.OrderBy(m => m.Value), generated.Npcs.Select(n => n.Id).OrderBy(id => id.Value));

        foreach (var household in generated.Households)
            Assert.Contains(household.Head, household.Members);
    }

    [Fact]
    public void Same_seed_generates_the_same_population_twice()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var a = PopulationGenerator.GenerateInitial(new WorldRng(42), now, 50, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);
        var b = PopulationGenerator.GenerateInitial(new WorldRng(42), now, 50, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.Equal(a.Npcs.Select(n => (n.Id, n.Sex, n.AgeYears(now))), b.Npcs.Select(n => (n.Id, n.Sex, n.AgeYears(now))));
        // Task 7: mesma seed produz mesma Personality/Profissão para o mesmo NPC.
        Assert.Equal(a.Npcs.Select(n => n.Personality), b.Npcs.Select(n => n.Personality));
        Assert.Equal(a.Npcs.Select(n => n.Profession), b.Npcs.Select(n => n.Profession));
        // Fase 16.3 COH-21: mesma seed → mesmos Height/Weight/MuscleMass.
        Assert.Equal(a.Npcs.Select(n => (n.Height, n.Weight, n.MuscleMass)),
            b.Npcs.Select(n => (n.Height, n.Weight, n.MuscleMass)));
    }

    [Fact]
    public void Generated_npcs_have_body_fields_within_BodyRules_range()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var rules = BodyRules.Default;
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(11), now, 50, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog,
            bodyRules: rules);

        Assert.All(generated.Npcs, n =>
        {
            Assert.InRange(n.Height, rules.HeightMin, rules.HeightMax);
            Assert.InRange(n.Weight, rules.WeightMin, rules.WeightMax);
            Assert.InRange(n.MuscleMass, rules.MuscleMassMin, rules.MuscleMassMax);
        });
    }

    [Fact]
    public void Different_seeds_produce_different_body_fields()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var a = PopulationGenerator.GenerateInitial(new WorldRng(100), now, 20, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);
        var b = PopulationGenerator.GenerateInitial(new WorldRng(101), now, 20, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.NotEqual(
            a.Npcs.Select(n => (n.Height, n.Weight, n.MuscleMass)),
            b.Npcs.Select(n => (n.Height, n.Weight, n.MuscleMass)));
    }

    [Fact]
    public void Different_seeds_produce_different_personalities()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var a = PopulationGenerator.GenerateInitial(new WorldRng(100), now, 20, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);
        var b = PopulationGenerator.GenerateInitial(new WorldRng(101), now, 20, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.NotEqual(a.Npcs.Select(n => n.Personality), b.Npcs.Select(n => n.Personality));
    }

    [Fact]
    public void Every_generated_npc_has_a_non_default_personality_and_profession()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(7), now, count: 30, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        // Catálogo vazio (sem restrição) resolve sempre para o sentinela "sem profissão"
        // (NEEDS-07/08/10 dependem só de Profession existir, nunca de default silencioso).
        Assert.All(generated.Npcs, n => Assert.Equal(ProfessionType.None, n.Profession));
        Assert.All(generated.Npcs, n => Assert.NotNull(n.Personality));
    }

    [Fact]
    public void Profession_is_drawn_uniformly_from_a_non_empty_catalog_without_throwing()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var catalog = new PopulationCatalog(new HashSet<int>(), new HashSet<int> { 1, 2, 3 }, new HashSet<int>());
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(8), now, count: 50, new CultureId(1), new CellCoord(5, 5), Table, catalog);

        Assert.All(generated.Npcs, n => Assert.True(catalog.IsValidProfession(n.Profession)));
        // Com 50 NPCs e 3 profissões, extremamente improvável (mas não impossível por
        // construção) que uma única profissão apareça 50/50 vezes — sinal de sorteio real.
        Assert.True(generated.Npcs.Select(n => n.Profession).Distinct().Count() > 1);
    }

    [Fact]
    public void Zero_count_produces_no_npcs_and_no_households()
    {
        var now = WorldDate.Epoch(Calendar);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(4), now, count: 0, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.Empty(generated.Npcs);
        Assert.Empty(generated.Households);
    }

    [Fact]
    public void Single_npc_forms_its_own_household_as_head()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(5), now, count: 1, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.Single(generated.Npcs);
        Assert.Single(generated.Households);
        Assert.Equal(generated.Npcs[0].Id, generated.Households[0].Head);
    }

    [Fact]
    public void Every_seed_npc_has_vitality_and_upbringing_in_range_without_known_parents()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(11), now, count: 40, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.All(generated.Npcs, n =>
        {
            Assert.InRange(n.Vitality, 0, 100);
            Assert.InRange(n.Upbringing, 0, 100);
            Assert.Null(n.MotherId);
            Assert.Null(n.FatherId);
        });
    }

    [Fact]
    public void Vitality_and_upbringing_are_deterministic_per_npc_id_for_same_seed()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var a = PopulationGenerator.GenerateInitial(
            new WorldRng(77), now, count: 25, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);
        var b = PopulationGenerator.GenerateInitial(
            new WorldRng(77), now, count: 25, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        Assert.Equal(
            a.Npcs.Select(n => (n.Id, n.Vitality, n.Upbringing)),
            b.Npcs.Select(n => (n.Id, n.Vitality, n.Upbringing)));
    }

    [Fact]
    public void Paired_adults_in_seed_population_are_marked_as_spouses()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(12), now, count: 20, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog);

        var withSpouse = generated.Npcs.Where(n => n.Spouse is not null).ToList();
        Assert.NotEmpty(withSpouse);
        foreach (var npc in withSpouse)
        {
            var spouse = generated.Npcs.Single(n => n.Id == npc.Spouse);
            Assert.Equal(npc.Id, spouse.Spouse);
        }
    }

    [Fact]
    public void Initial_households_are_distributed_deterministically_across_supplied_spawn_cells()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        CellCoord[] spawnCells = Enumerable.Range(0, 30).Select(x => new CellCoord(x, 4)).ToArray();

        var a = PopulationGenerator.GenerateInitial(
            new WorldRng(21), now, 30, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog,
            householdLocationsFactory: count => spawnCells.Take(count).ToArray());
        var b = PopulationGenerator.GenerateInitial(
            new WorldRng(21), now, 30, new CultureId(1), new CellCoord(5, 5), Table, EmptyCatalog,
            householdLocationsFactory: count => spawnCells.Take(count).ToArray());

        Assert.Equal(a.Households.Count, a.Households.Select(h => h.Location).Distinct().Count());
        Assert.Equal(a.Npcs.Select(n => n.CurrentLocation), b.Npcs.Select(n => n.CurrentLocation));
        Assert.All(a.Households, household => Assert.Contains(household.Location, spawnCells));
    }

    [Fact]
    public void Population_seeder_places_every_house_footprint_on_valid_map_cells()
    {
        var map = ScenarioRunner.InitialMap(seed: 31, initialPopulation: 30);
        var world = new WorldState(
            Calendar, 31, map, ScenarioRunner.DefaultPopulationCatalog,
            ScenarioRunner.DefaultPopulationRules, ScenarioRunner.DefaultNeedsRules,
            ScenarioRunner.DefaultActionCatalog, ScenarioRunner.DefaultLifeStageRules);
        var village = new CellCoord(5, 5);

        PopulationSeeder.SeedInitial(world, 30, new CultureId(1), village);

        Assert.All(world.Buildings.SelectMany(building =>
        {
            var origin = building.Position!.Value;
            return BuildingFootprintGenerator.Generate(building.Id, building.BuildingTypeId)
                .Select(cell => new CellCoord(origin.X + cell.Cell.X, origin.Y + cell.Cell.Y));
        }), cell => Assert.True(map.TryGetCell(cell, out _)));
    }
}
