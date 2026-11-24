using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy.Labor;

/// <summary>AD-148 (backlog 04, E1): a fazenda é o mercado da vila. Quem está nela na fila da compra não lavra; só conta para a
/// produção quem está trabalhando. Antes bastava estar no local, e a fila do mercado contava como mão na lavoura.</summary>
public class PresenceNeedsWorkTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void Only_who_is_working_at_the_farm_counts_as_present(bool presenceNeedsWork, int expected)
    {
        var economy = ScenarioRunner.DefaultEconomyRules with { PresenceNeedsWork = presenceNeedsWork };
        var (world, clock) = ScenarioRunner.Create(1, economyRules: economy);
        clock.Run(world, 48); // contratação diária
        var farm = world.Workplaces.First(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
        var staff = farm.Employees.Take(2).Select(id => world.FindNpc(id)!).ToList();
        long now = world.CurrentDate.TotalHours;
        foreach (var npc in world.Workplaces.SelectMany(w => w.Employees).Select(id => world.FindNpc(id)!).Where(n => n.CurrentLocation == farm.Location))
            npc.MoveTo(new(farm.Location.X + 5, farm.Location.Y + 5), now);
        foreach (var npc in staff) npc.MoveTo(farm.Location, now);
        staff[0].SetCurrentAction(ActionType.Work, now);
        staff[1].SetCurrentAction(ActionType.Buy, now);

        var present = VacancyIndex.BuildForTick(world).PresentWorkersAt(farm);

        Assert.Equal(expected, present.Count);
        Assert.Contains(staff[0], present);
    }
}
