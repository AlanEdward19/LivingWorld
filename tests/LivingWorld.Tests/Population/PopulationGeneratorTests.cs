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

    [Fact]
    public void Initial_population_is_not_all_the_same_age_nor_all_adults()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200); // margem para não truncar crianças no ano 0
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(1), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table);

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
            new WorldRng(2), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table);

        Assert.Contains(generated.Npcs, n => n.Sex == Sex.Female);
        Assert.Contains(generated.Npcs, n => n.Sex == Sex.Male);
    }

    [Fact]
    public void Every_generated_npc_belongs_to_exactly_one_household_and_every_head_is_a_member()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(3), now, count: 100, new CultureId(1), new CellCoord(5, 5), Table);

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
        var a = PopulationGenerator.GenerateInitial(new WorldRng(42), now, 50, new CultureId(1), new CellCoord(5, 5), Table);
        var b = PopulationGenerator.GenerateInitial(new WorldRng(42), now, 50, new CultureId(1), new CellCoord(5, 5), Table);

        Assert.Equal(a.Npcs.Select(n => (n.Id, n.Sex, n.AgeYears(now))), b.Npcs.Select(n => (n.Id, n.Sex, n.AgeYears(now))));
    }

    [Fact]
    public void Zero_count_produces_no_npcs_and_no_households()
    {
        var now = WorldDate.Epoch(Calendar);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(4), now, count: 0, new CultureId(1), new CellCoord(5, 5), Table);

        Assert.Empty(generated.Npcs);
        Assert.Empty(generated.Households);
    }

    [Fact]
    public void Single_npc_forms_its_own_household_as_head()
    {
        var now = WorldDate.Epoch(Calendar).AddYears(200);
        var generated = PopulationGenerator.GenerateInitial(
            new WorldRng(5), now, count: 1, new CultureId(1), new CellCoord(5, 5), Table);

        Assert.Single(generated.Npcs);
        Assert.Single(generated.Households);
        Assert.Equal(generated.Npcs[0].Id, generated.Households[0].Head);
    }
}
