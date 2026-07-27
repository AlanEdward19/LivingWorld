using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

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
}
