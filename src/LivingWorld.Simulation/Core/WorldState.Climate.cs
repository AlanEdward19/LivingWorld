using LivingWorld.Domain.Climate;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Snapshot;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState: anos de safra por região (weather-drought).
public sealed partial class WorldState
{
    /// <summary>Anos que o livro de clima guarda além do corrente; os mais velhos saem.</summary>
    public const int ClimateYearsKept = 3;

    public long ClimateYearOf(long tick) => tick / (CurrentDate.Calendar.HoursPerMonth * CurrentDate.Calendar.MonthsPerYear);

    /// <summary>Fator de safra da região no ano, em milésimos: o sorteado, ou 1000 (ano médio) com clima desligado ou sem
    /// sorteio ainda.</summary>
    public int ClimateFactorMilli(RegionId region, long year)
    {
        if (!ClimateRules.Enabled) return 1000;
        int index = _climateYears.FindIndex(c => c.Region == region && c.Year == year);
        return index < 0 ? 1000 : _climateYears[index].FactorMilli;
    }

    /// <summary>Fator de safra no ponto e no tick; com clima desligado volta 1000 sem tocar no mapa (caminho quente da colheita),
    /// e um ponto fora de qualquer região vale 1000 em vez de estourar.</summary>
    public int ClimateFactorAt(CellCoord at, long tick) =>
        !ClimateRules.Enabled || !Map.TryGetBaseClimate(at, out _, out var region) ? 1000 : ClimateFactorMilli(region, ClimateYearOf(tick));

    /// <summary>Grava o fator do (região, ano); um fator por par, nunca reescrito depois de sorteado.</summary>
    public void SetClimateYear(RegionId region, long year, int factorMilli)
    {
        if (_climateYears.Any(c => c.Region == region && c.Year == year)) return;
        var created = new ClimateYear(region, year, factorMilli);
        int at = _climateYears.BinarySearch(created, Comparer<ClimateYear>.Create(
            (a, b) => a.Region.Value != b.Region.Value ? a.Region.Value.CompareTo(b.Region.Value) : a.Year.CompareTo(b.Year)));
        _climateYears.Insert(at < 0 ? ~at : at, created);
        CanonicalHashCache.MarkPropertyDirty(nameof(ClimateYears));
    }

    public void PurgeClimateYearsBefore(long year)
    {
        if (_climateYears.RemoveAll(c => c.Year < year) > 0)
            CanonicalHashCache.MarkPropertyDirty(nameof(ClimateYears));
    }
}
