using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Population.Family;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Tests.Unit.Population.Family;

/// <summary>Só casa quem tem do que viver (AD-158): a terra demarcada comporta um número de posses; com todas tomadas, nem o
/// impulsivo casa, até uma vagar; o artesão (ofício fora da lavoura) e quem já chefia a própria casa casam sem posse nova.</summary>
public class HoldingTests
{
    private static readonly Personality Reckless = Personality.Create(50, 50, 0, 50, 50, 50, 50, 50, 100, 0).Value!;

    private sealed class Sink : IWorldEventSink
    {
        public List<WorldEvent> Events { get; } = [];
        public void Record(WorldEvent evt) => Events.Add(evt);
    }

    /// <summary>A vila do cenário de código com a terra cortada para caber exatamente quem vive dela hoje (+ <paramref name="spare"/>
    /// posses, menos uma semente), e um casal jovem e impulsivo que mora com os pais dele e dela.</summary>
    private static (WorldState World, Npc Him, Npc Her) Village(int spare, FamilyRules? family = null)
    {
        var (world, _) = ScenarioRunner.Create(7, familyRules: family);
        var homes = world.Households.Where(h => !h.IsEmpty).OrderBy(h => h.Id.Value).ToList();
        var him = Person(world, Sex.Male, 22, homes[0]);
        var her = Person(world, Sex.Female, 20, homes[1]);
        var rules = HoldingRules.Default;
        double inUse = Holdings.LandInUse(world, him.City, rules);
        var field = world.CropBatches.Single(c => c.ArableSeed > 0);
        field.SetArable((long)Math.Ceiling(inUse + (spare + 1) * Holdings.SeedPerHolding(world, rules)) - 1);
        return (world, him, her);
    }

    private static Npc Person(WorldState world, Sex sex, int age, Household home)
    {
        var npc = new Npc(
            world.NextNpcIdAndAdvance(), "npc", sex, world.CurrentDate.AddYears(-age), new CultureId(1), home.Location,
            motherId: null, fatherId: null, household: null, health: 100, personality: Reckless,
            profession: new ProfessionType(1), currentLocation: home.Location, wallet: new Money(5_000), city: home.City);
        world.AddNpc(npc);
        home.AddMember(npc.Id);
        npc.JoinHousehold(home.Id);
        return npc;
    }

    [Fact]
    public void With_every_holding_taken_not_even_the_reckless_with_savings_marry()
    {
        var (world, him, her) = Village(spare: 0);

        Assert.Equal(0, Holdings.Vacant(world, him.City));
        Assert.False(Holdings.HasLivelihood(world, him, her));
        Assert.Equal(0.0, HouseholdMeans.MarriageReadiness(world, him, her));
        Assert.Equal(0.0, HouseholdMeans.MarriageReadiness(world, her, him));
    }

    [Fact]
    public void A_vacant_holding_lets_them_marry_and_the_young_couple_takes_a_whole_holding()
    {
        var (world, him, her) = Village(spare: 1);
        var rules = HoldingRules.Default;
        Assert.Equal(1, Holdings.Vacant(world, him.City));
        Assert.True(Holdings.HasLivelihood(world, him, her));
        double before = Holdings.LandInUse(world, him.City, rules);

        MarriageSystem.Marry(world, new TickContext(world, world.Rng, world.Scheduler, new Sink()), him, her);

        Assert.True(Holdings.LandInUse(world, him.City, rules) > before);
    }

    [Fact]
    public void A_family_that_dies_out_gives_its_land_back()
    {
        var (world, him, her) = Village(spare: 0);
        var ctx = new TickContext(world, world.Rng, world.Scheduler, new Sink());
        var lastHome = world.Households.Where(h => !h.IsEmpty && h.Id != him.Household && h.Id != her.Household)
            .OrderByDescending(h => h.Id.Value).First();
        foreach (var member in lastHome.Members.ToList()) lastHome.RemoveMember(member);
        HouseholdCleanup.DissolveIfEmpty(world, ctx, lastHome);

        Assert.True(Holdings.HasLivelihood(world, him, her));
    }

    [Fact]
    public void A_craftsman_with_a_wage_marries_without_land()
    {
        var (world, him, her) = Village(spare: 0);
        var forge = world.Workplaces.First(w => world.FindCropAt(w.Location) is null);
        him.SwitchProfession(new ProfessionType(2));
        him.Hire(forge.Id);

        Assert.True(Holdings.HasCraft(world, him));
        Assert.True(Holdings.HasLivelihood(world, him, her));
    }

    [Fact]
    public void Without_the_rule_a_full_village_does_not_stop_them()
    {
        var family = ScenarioRunner.DefaultFamilyRules with { Pressure = PopulationPressureRules.Measured with { Holdings = null } };
        var (world, him, her) = Village(spare: 0, family);

        Assert.True(Holdings.HasLivelihood(world, him, her));
    }

    [Fact]
    public void Courtship_that_ends_with_no_livelihood_is_called_off()
    {
        var (world, him, her) = Village(spare: 0);
        var sink = new Sink();
        var ctx = new TickContext(world, world.Rng, world.Scheduler, sink);
        him.StartCourtship(her.Id);
        her.StartCourtship(him.Id);
        var (a, b) = him.Id.Value < her.Id.Value ? (him, her) : (her, him);

        new CourtshipSystem().HandleEvent(world, ctx, new ScheduledEvent(1, world.CurrentDate.TotalHours, CourtshipSystem.SystemName, $"{a.Id.Value}|{b.Id.Value}"));

        Assert.Null(him.Spouse);
        Assert.Null(him.CourtingWith);
        Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.CourtshipRejected && e.Payload.StartsWith("SemMeios|", StringComparison.Ordinal));
    }
}
