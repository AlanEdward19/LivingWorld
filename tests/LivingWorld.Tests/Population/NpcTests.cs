using LivingWorld.Domain;

namespace LivingWorld.Tests.Population;

public class NpcTests
{
    private static readonly WorldCalendar Calendar = new(24, 30, 12);

    private static Npc MakeNpc(WorldDate birthDate) => new(
        new NpcId(1), "test", Sex.Female, birthDate, new CultureId(1), new CellCoord(0, 0),
        motherId: null, fatherId: null, household: null, health: 100);

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
            null, null, null, health: 101));
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
}
