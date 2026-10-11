using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Economy.Labor;

/// <summary>Só o peso de vagas por tipo de local (a parte de <see cref="VacancyIndex"/> que a troca de
/// profissão consome), sem listas de empregados nem ordenação: o mesmo número, a custo O(workplaces).</summary>
public sealed class VacancyWeights
{
    private readonly Dictionary<int, (int Open, int Total)> _slotsByLocationType;

    private VacancyWeights(Dictionary<int, (int Open, int Total)> slotsByLocationType) =>
        _slotsByLocationType = slotsByLocationType;

    public double VacancyWeightForLocationType(int locationTypeId)
    {
        if (!_slotsByLocationType.TryGetValue(locationTypeId, out var slots) || slots.Total == 0)
            return 1.0;
        return 1.0 + (double)slots.Open / slots.Total;
    }

    public static VacancyWeights BuildForTick(WorldState world)
    {
        var slotsByType = new Dictionary<int, (int Open, int Total)>();
        foreach (var wp in world.Workplaces)
        {
            int typeId = wp.LocationType.Id;
            int open = Math.Max(0, wp.MaxVacancies - wp.Employees.Count);
            if (slotsByType.TryGetValue(typeId, out var prev))
                slotsByType[typeId] = (prev.Open + open, prev.Total + wp.MaxVacancies);
            else
                slotsByType[typeId] = (open, wp.MaxVacancies);
        }

        return new VacancyWeights(slotsByType);
    }
}
