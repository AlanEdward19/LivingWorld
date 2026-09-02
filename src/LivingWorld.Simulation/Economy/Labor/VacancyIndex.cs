using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Vagas abertas e workplaces com vaga por tipo de local, recomputado uma vez por dia (PERF-06).</summary>
public sealed class VacancyIndex
{
    private readonly Dictionary<int, (int Open, int Total)> _slotsByLocationType;
    private readonly Dictionary<int, Workplace?> _firstOpenByLocationType;
    private readonly Dictionary<(int LocationTypeId, CityId City), Workplace?> _firstOpenByLocationTypeAndCity;
    private readonly Dictionary<WorkplaceId, List<Npc>> _employeesByWorkplace;

    private VacancyIndex(
        Dictionary<int, (int Open, int Total)> slotsByLocationType,
        Dictionary<int, Workplace?> firstOpenByLocationType,
        Dictionary<(int LocationTypeId, CityId City), Workplace?> firstOpenByLocationTypeAndCity,
        Dictionary<WorkplaceId, List<Npc>> employeesByWorkplace)
    {
        _slotsByLocationType = slotsByLocationType;
        _firstOpenByLocationType = firstOpenByLocationType;
        _firstOpenByLocationTypeAndCity = firstOpenByLocationTypeAndCity;
        _employeesByWorkplace = employeesByWorkplace;
    }

    public Workplace? FirstWorkplaceWithVacancy(int locationTypeId) =>
        _firstOpenByLocationType.TryGetValue(locationTypeId, out var wp) ? wp : null;

    /// <summary>Mesma busca acima, mas restrita a workplaces de <paramref name="city"/> — usado
    /// pela contratação (ghost-town fix) pra nunca contratar um NPC pra fora da própria cidade.</summary>
    public Workplace? FirstWorkplaceWithVacancy(int locationTypeId, CityId city) =>
        _firstOpenByLocationTypeAndCity.TryGetValue((locationTypeId, city), out var wp) ? wp : null;

    public double VacancyWeightForLocationType(int locationTypeId)
    {
        if (!_slotsByLocationType.TryGetValue(locationTypeId, out var slots) || slots.Total == 0)
            return 1.0;
        return 1.0 + (double)slots.Open / slots.Total;
    }

    public IReadOnlyList<Npc> PresentWorkersAt(Workplace workplace)
    {
        if (!_employeesByWorkplace.TryGetValue(workplace.Id, out var employees))
            return [];

        var present = new List<Npc>(employees.Count);
        foreach (var npc in employees)
        {
            if (npc.IsAlive && npc.CurrentLocation == workplace.Location)
                present.Add(npc);
        }
        return present;
    }

    public static VacancyIndex BuildForTick(WorldState world)
    {
        var slotsByType = new Dictionary<int, (int Open, int Total)>();
        var firstByType = new Dictionary<int, Workplace?>();
        var firstByTypeAndCity = new Dictionary<(int LocationTypeId, CityId City), Workplace?>();
        var employeesByWorkplace = new Dictionary<WorkplaceId, List<Npc>>();

        foreach (var wp in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            int typeId = wp.LocationType.Id;
            int open = Math.Max(0, wp.MaxVacancies - wp.Employees.Count);
            if (slotsByType.TryGetValue(typeId, out var prev))
                slotsByType[typeId] = (prev.Open + open, prev.Total + wp.MaxVacancies);
            else
                slotsByType[typeId] = (open, wp.MaxVacancies);

            if (open > 0)
            {
                firstByType.TryAdd(typeId, wp);
                firstByTypeAndCity.TryAdd((typeId, wp.City), wp);
            }

            var list = new List<Npc>(wp.Employees.Count);
            foreach (var empId in wp.Employees)
            {
                if (world.FindNpc(empId) is { } npc)
                    list.Add(npc);
            }
            employeesByWorkplace[wp.Id] = list;
        }

        return new VacancyIndex(slotsByType, firstByType, firstByTypeAndCity, employeesByWorkplace);
    }
}
