using LivingWorld.Domain;

namespace LivingWorld.Tests;

public class WorldDateTests
{
    private static readonly WorldCalendar Calendar = new(HoursPerDay: 24, DaysPerMonth: 30, MonthsPerYear: 12);

    [Fact]
    public void Epoch_starts_at_year_zero()
    {
        var date = WorldDate.Epoch(Calendar);
        Assert.Equal(0, date.Year);
        Assert.Equal(0, date.Month);
        Assert.Equal(0, date.Day);
        Assert.Equal(0, date.Hour);
    }

    [Fact]
    public void AddHours_rolls_over_into_day()
    {
        var date = WorldDate.Epoch(Calendar).AddHours(24);
        Assert.Equal(1, date.Day);
        Assert.Equal(0, date.Hour);
    }

    [Fact]
    public void AddDays_rolls_over_into_month()
    {
        var date = WorldDate.Epoch(Calendar).AddDays(30);
        Assert.Equal(1, date.Month);
        Assert.Equal(0, date.Day);
    }

    [Fact]
    public void AddMonths_rolls_over_into_year()
    {
        var date = WorldDate.Epoch(Calendar).AddMonths(12);
        Assert.Equal(1, date.Year);
        Assert.Equal(0, date.Month);
    }

    [Fact]
    public void AddYears_advances_year_only()
    {
        var date = WorldDate.Epoch(Calendar).AddYears(5);
        Assert.Equal(5, date.Year);
    }

    [Fact]
    public void Comparison_operators_follow_total_hours()
    {
        var early = WorldDate.Epoch(Calendar);
        var late = early.AddHours(1);
        Assert.True(early < late);
        Assert.True(late > early);
        Assert.True(early <= late);
        Assert.True(late >= early);
    }

    [Fact]
    public void Different_calendars_reach_year_boundary_at_different_hour_counts()
    {
        var shortYear = new WorldCalendar(HoursPerDay: 1, DaysPerMonth: 40, MonthsPerYear: 8);
        var date = WorldDate.Epoch(shortYear).AddHours(shortYear.HoursPerYear);
        Assert.Equal(1, date.Year);
        Assert.Equal(0, date.Month);
    }
}
