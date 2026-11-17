using LivingWorld.Api.Visual.Observable;
using LivingWorld.Api.Visual.Scope;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Integration.Visual;

/// <summary>AD-170: envolvidos estruturados dos eventos, lidos do payload que o motor já grava.</summary>
public class EventInvolvementTests
{
    private static readonly WorldState World = ScenarioRunner.Create(42, initialPopulation: 20).World;

    private static EventInvolved Read(WorldEventKind kind, string payload) =>
        EventInvolvementReader.Read(World, new WorldEvent(10, kind, payload));

    [Fact]
    public void Support_paid_names_payer_receiver_and_coins()
    {
        var involved = Read(WorldEventKind.SupportPaid, "3|7|12|500|2");

        Assert.Equal(((long?)3, (long?)7, (long?)500), (involved.SubjectId, involved.OtherId, involved.Amount));
    }

    [Fact]
    public void Theft_names_author_victim_case_and_outcome_without_a_description()
    {
        var involved = Read(WorldEventKind.TheftCommitted, "3|7|casa|fome|moeda|5|5|levou|0|42");

        Assert.Equal(((long?)3, (long?)7, (long?)42, "levou"), (involved.AuthorId, involved.VictimId, involved.CaseId, involved.Outcome));
    }

    [Fact]
    public void Theft_from_the_city_has_no_victim_person()
    {
        Assert.Null(Read(WorldEventKind.TheftCommitted, "3|-1|caixa|fome|moeda|5|5|levou|0|42").VictimId);
    }

    [Theory]
    [InlineData(WorldEventKind.SexualAssault, "3|7|adulto|0|9|0")]
    [InlineData(WorldEventKind.DomesticViolence, "3|7|conjuge|ciume|10|ferido|0|9")]
    [InlineData(WorldEventKind.Kidnapped, "3|7|posse|crianca|0|0|9")]
    public void Sexual_domestic_and_child_violence_are_record_only(WorldEventKind kind, string payload)
    {
        Assert.True(Read(kind, payload).RecordOnly);
    }

    [Fact]
    public void Plain_assault_on_an_adult_is_not_record_only()
    {
        long adult = World.Npcs.First(n => n.IsAlive && n.AgeYears(World.CurrentDate) > 20).Id.Value;

        Assert.False(Read(WorldEventKind.Assault, $"3|{adult}|ofensa|10|ferido|0|9").RecordOnly);
    }

    [Fact]
    public void Birth_names_baby_parents_and_household()
    {
        var involved = Read(WorldEventKind.Birth, "30|3|4|8");

        Assert.Equal(((long?)30, "3,4", (long?)8), (involved.SubjectId, string.Join(',', involved.RelatedIds!), involved.HouseholdId));
    }

    [Fact]
    public void Leavings_left_is_placed_on_the_cell_where_they_lie()
    {
        var evt = new WorldEvent(10, WorldEventKind.LeavingsLeft, "5|3|8|-1|12|9|4|0");

        var visual = Assert.Single(LivingScopeProjector.Build(World, new VisualScope(VisualScopeKind.World, ""), [evt]).Events,
            e => e.Kind == WorldEventKind.LeavingsLeft);

        Assert.Equal((new CellCoord(12, 9), (long?)5), (visual.Location!.Value, visual.Involved!.LeavingId));
    }

    [Fact]
    public void Scope_event_carries_its_log_id()
    {
        var evt = new WorldEvent(10, WorldEventKind.Marriage, "3|4", EventId: 77);

        var visual = Assert.Single(LivingScopeProjector.Build(World, new VisualScope(VisualScopeKind.World, ""), [evt]).Events,
            e => e.Kind == WorldEventKind.Marriage);

        Assert.Equal(77, visual.EventId);
    }

    [Fact]
    public void Rent_unpaid_is_placed_at_the_household_not_at_a_person_with_that_id()
    {
        var home = World.Households.First(h => !h.IsEmpty);
        var evt = new WorldEvent(10, WorldEventKind.RentUnpaid, $"{home.Id.Value}|1|50|1");

        Assert.Equal(home.Location, EventInvolvementReader.LocationOf(World, evt, EventInvolvementReader.Read(World, evt)));
    }
}
