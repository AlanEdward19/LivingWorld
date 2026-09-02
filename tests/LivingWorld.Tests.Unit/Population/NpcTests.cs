using System.Text.Json;
using System.Text.Json.Serialization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Tests.Unit.Population;

public class NpcTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static readonly Personality DefaultPersonality =
        Personality.Create(50, 50, 50, 50, 50, 50, 50, 50, 50, 50).Value!;

    private static Npc MakeNpc(WorldDate birthDate) => new(
        new NpcId(1), "test", Sex.Female, birthDate, new CultureId(1), new CellCoord(0, 0),
        motherId: null, fatherId: null, household: null, health: 100,
        personality: DefaultPersonality, profession: default, currentLocation: new CellCoord(0, 0));

    [Fact]
    public void Age_is_derived_from_birth_date_never_stored()
    {
        var birth = WorldDate.Epoch(Calendar);
        var npc = MakeNpc(birth);

        Assert.Equal(0, npc.AgeYears(birth));
        Assert.Equal(5, npc.AgeYears(birth.AddYears(5)));
    }

    [Fact]
    public void Age_freezes_at_death()
    {
        var birth = WorldDate.Epoch(Calendar);
        var npc = MakeNpc(birth);
        npc.Die(birth.AddYears(40));

        Assert.Equal(40, npc.AgeYears(birth.AddYears(100)));
    }

    [Fact]
    public void Health_outside_zero_hundred_is_rejected_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Npc(
            new NpcId(1), "x", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
            null, null, null, health: 101, personality: DefaultPersonality, profession: default,
            currentLocation: new CellCoord(0, 0)));
    }

    [Fact]
    public void Dying_twice_is_rejected()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.Die(WorldDate.Epoch(Calendar).AddYears(1));
        Assert.Throws<InvalidOperationException>(() => npc.Die(WorldDate.Epoch(Calendar).AddYears(2)));
    }

    [Fact]
    public void Is_alive_reflects_death_date_and_is_not_serialized()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        Assert.True(npc.IsAlive);
        npc.Die(WorldDate.Epoch(Calendar).AddYears(1));
        Assert.False(npc.IsAlive);
    }

    // --- Fase 4 (task 6): necessidades, personalidade, profissão, localização, homeless ---

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(0, 0)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void SetHunger_clamps_to_zero_hundred(int input, int expected)
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetHunger(input);
        Assert.Equal(expected, npc.Hunger);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(150, 100)]
    public void SetThirst_clamps_to_zero_hundred(int input, int expected)
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetThirst(input);
        Assert.Equal(expected, npc.Thirst);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(150, 100)]
    public void SetSleep_clamps_to_zero_hundred(int input, int expected)
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetSleep(input);
        Assert.Equal(expected, npc.Sleep);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(150, 100)]
    public void SetSocial_clamps_to_zero_hundred(int input, int expected)
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetSocial(input);
        Assert.Equal(expected, npc.Social);
    }

    [Fact]
    public void HomelessSince_is_null_while_household_exists_and_set_when_it_leaves()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.JoinHousehold(new HouseholdId(1));
        Assert.Null(npc.HomelessSince);

        var now = WorldDate.Epoch(Calendar).AddYears(1);
        npc.LeaveHousehold(now);
        Assert.Equal(now, npc.HomelessSince);
        Assert.Null(npc.Household);
    }

    [Fact]
    public void JoinHousehold_clears_HomelessSince()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.LeaveHousehold(WorldDate.Epoch(Calendar).AddYears(1));
        Assert.NotNull(npc.HomelessSince);

        npc.JoinHousehold(new HouseholdId(2));
        Assert.Null(npc.HomelessSince);
    }

    [Fact]
    public void MoveTo_updates_current_location()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        Assert.Equal(new CellCoord(0, 0), npc.CurrentLocation); // MakeNpc passa o mesmo valor de BirthLocation

        npc.MoveTo(new CellCoord(3, 4), tick: 10);
        Assert.Equal(new CellCoord(3, 4), npc.CurrentLocation);
    }

    [Fact]
    public void SetCurrentAction_sets_action_and_start_tick()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetCurrentAction(ActionType.Work, tick: 42);
        Assert.Equal(ActionType.Work, npc.CurrentAction);
        Assert.Equal(42, npc.ActionStartedAtTick);
    }

    // --- Fase 16.3 T25 (COH-41): Intent persistence ---

    [Fact]
    public void SetIntent_sets_Active_status_and_fields()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetIntent(ActionType.Buy, tick: 10, target: "market:1");

        Assert.Equal(ActionType.Buy, npc.CurrentIntent);
        Assert.Equal(10, npc.IntentStartedTick);
        Assert.Equal("market:1", npc.IntentTarget);
        Assert.Equal(IntentStatus.Active, npc.IntentStatus);
    }

    [Fact]
    public void CompleteIntent_transitions_Active_to_Completed()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetIntent(ActionType.Eat, tick: 1);
        npc.CompleteIntent();

        Assert.Equal(IntentStatus.Completed, npc.IntentStatus);
        Assert.Equal(ActionType.Eat, npc.CurrentIntent);
    }

    [Fact]
    public void InvalidateIntent_transitions_Active_to_Invalidated()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetIntent(ActionType.Buy, tick: 1);
        npc.InvalidateIntent();

        Assert.Equal(IntentStatus.Invalidated, npc.IntentStatus);
    }

    [Fact]
    public void CompleteIntent_is_noop_when_not_Active()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetIntent(ActionType.Buy, tick: 1);
        npc.InvalidateIntent();
        npc.CompleteIntent();

        Assert.Equal(IntentStatus.Invalidated, npc.IntentStatus);
    }

    [Fact]
    public void Intent_fields_round_trip_through_json()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.SetIntent(ActionType.Buy, tick: 77, target: "food");

        var json = JsonSerializer.Serialize(npc, options);
        var rehydrated = JsonSerializer.Deserialize<Npc>(json, options)!;

        Assert.Equal(ActionType.Buy, rehydrated.CurrentIntent);
        Assert.Equal(77, rehydrated.IntentStartedTick);
        Assert.Equal("food", rehydrated.IntentTarget);
        Assert.Equal(IntentStatus.Active, rehydrated.IntentStatus);
    }

    // Npc não tem snapshot próprio (WorldState/WorldSnapshot é quem serializa de verdade, T9) —
    // round-trip direto via System.Text.Json sobre o tipo isolado, mesmas opções (enum-como-string)
    // usadas por WorldSnapshot, prova que o construtor único continua reidratando todo campo novo.
    [Fact]
    public void Round_trip_via_json_preserves_needs_personality_profession_and_action_fields()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var personality = Personality.Create(10, 20, 30, 40, 50, 60, 70, 80, 90, 100).Value!;
        var npc = new Npc(
            new NpcId(7), "round-trip", Sex.Male, WorldDate.Epoch(Calendar), new CultureId(2), new CellCoord(1, 1),
            motherId: null, fatherId: null, household: new HouseholdId(3), health: 80,
            personality: personality, profession: new ProfessionType(9),
            hunger: 33, thirst: 44, sleep: 55, social: 66,
            currentLocation: new CellCoord(5, 6), currentAction: ActionType.Socialize, actionStartedAtTick: 12,
            hungerZeroSinceTick: 8, homelessSince: WorldDate.Epoch(Calendar).AddYears(2),
            wallet: new Money(250), employer: new WorkplaceId(4),
            city: new CityId(Guid.Parse("00000000-0000-0000-0000-0000000000aa")));

        var json = JsonSerializer.Serialize(npc, options);
        var rehydrated = JsonSerializer.Deserialize<Npc>(json, options)!;

        Assert.Equal(npc.Hunger, rehydrated.Hunger);
        Assert.Equal(npc.Thirst, rehydrated.Thirst);
        Assert.Equal(npc.Sleep, rehydrated.Sleep);
        Assert.Equal(npc.Social, rehydrated.Social);
        Assert.Equal(npc.Personality, rehydrated.Personality);
        Assert.Equal(npc.Profession, rehydrated.Profession);
        Assert.Equal(npc.CurrentLocation, rehydrated.CurrentLocation);
        Assert.Equal(npc.CurrentAction, rehydrated.CurrentAction);
        Assert.Equal(npc.ActionStartedAtTick, rehydrated.ActionStartedAtTick);
        Assert.Equal(npc.HungerZeroSinceTick, rehydrated.HungerZeroSinceTick);
        Assert.Equal(npc.HomelessSince, rehydrated.HomelessSince);
        Assert.Equal(npc.Wallet, rehydrated.Wallet);
        Assert.Equal(npc.Employer, rehydrated.Employer);
        Assert.Equal(npc.City, rehydrated.City);
    }

    [Fact]
    public void Height_Weight_MuscleMass_round_trip_through_json()
    {
        var npc = new Npc(
            new NpcId(1), "test", Sex.Female, WorldDate.Epoch(Calendar), new CultureId(1), new CellCoord(0, 0),
            motherId: null, fatherId: null, household: null, health: 100,
            personality: DefaultPersonality, profession: default, currentLocation: new CellCoord(0, 0),
            height: 1.82, weight: 74.5, muscleMass: 32.1);

        var json = JsonSerializer.Serialize(npc);
        var rehydrated = JsonSerializer.Deserialize<Npc>(json)!;

        Assert.Equal(1.82, rehydrated.Height);
        Assert.Equal(74.5, rehydrated.Weight);
        Assert.Equal(32.1, rehydrated.MuscleMass);
    }

    // --- Fase 8 (T4): CityId ---

    [Fact]
    public void JoinCity_changes_the_npc_city()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        var city = new CityId(Guid.Parse("00000000-0000-0000-0000-0000000000bb"));

        npc.JoinCity(city);

        Assert.Equal(city, npc.City);
    }

    // --- Fase 5 (T5): Wallet/Employer ---

    [Fact]
    public void TryDebitWallet_delegates_to_Money_TryDebit_and_decreases_wallet_on_success()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.CreditWallet(new Money(100));

        var result = npc.TryDebitWallet(new Money(40));

        Assert.True(result.IsSuccess);
        Assert.Equal(new Money(60), npc.Wallet);
    }

    [Fact]
    public void TryDebitWallet_fails_and_never_goes_negative_when_insufficient()
    {
        var npc = MakeNpc(WorldDate.Epoch(Calendar));
        npc.CreditWallet(new Money(10));

        var result = npc.TryDebitWallet(new Money(40));

        Assert.False(result.IsSuccess);
        Assert.Equal(new Money(10), npc.Wallet); // byte-idêntico ao estado anterior à tentativa
    }
}
