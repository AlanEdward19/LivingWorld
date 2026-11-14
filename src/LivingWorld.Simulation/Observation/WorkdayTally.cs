using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Simulation.Observation;

/// <summary>Onde vai o dia do adulto (AD-148): horas em Trabalhar e em Comprar (no mercado ou a caminho dele) por adulto de 16 a
/// 64 anos, e o lavrador às 10 h (a hora da produção): trabalhando na fazenda ou comprando. Amostra de hora em hora; só lê.</summary>
public sealed class WorkdayTally(WorldState world)
{
    public long MeasureFromTick { get; init; }
    public int HoursPerDay => world.Calendar.HoursPerDay;
    public double AdultHours, WorkHours, BuyHours;
    public long FarmerDays, FarmerWork, FarmerBuy;

    public void SampleHour()
    {
        long now = world.CurrentDate.TotalHours;
        if (now < MeasureFromTick) return;
        bool midshift = world.CurrentDate.Hour == world.Calendar.HoursPerDay * 10 / 24;
        Domain.Economy.Workplace? farm = null;
        if (midshift)
            foreach (var w in world.Workplaces)
                if (w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId) { farm = w; break; }
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            int age = npc.AgeYears(world.CurrentDate);
            if (age is < 16 or > 64) continue;
            AdultHours++;
            var act = npc.CurrentAction;
            bool buying = act == ActionType.Buy || act == ActionType.Travel && npc.TravelPurpose == ActionType.Buy;
            if (act == ActionType.Work) WorkHours++;
            else if (buying) BuyHours++;
            if (farm is null || npc.Employer != farm.Id) continue;
            FarmerDays++;
            if (act == ActionType.Work && npc.CurrentLocation == farm.Location) FarmerWork++;
            else if (buying) FarmerBuy++;
        }
    }

    public static string Summarize(IReadOnlyList<WorkdayTally> runs)
    {
        int hoursPerDay = runs.Count == 0 ? 24 : runs[0].HoursPerDay;
        var inv = CultureInfo.InvariantCulture;
        double adultDays = runs.Sum(r => r.AdultHours) / hoursPerDay;
        long farmer = runs.Sum(r => r.FarmerDays), work = runs.Sum(r => r.FarmerWork), buy = runs.Sum(r => r.FarmerBuy);
        var sb = new StringBuilder();
        sb.AppendLine(inv, $"dia_do_adulto h_trabalho/adulto/dia={runs.Sum(r => r.WorkHours) / Math.Max(1, adultDays):F2} h_compra/adulto/dia={runs.Sum(r => r.BuyHours) / Math.Max(1, adultDays):F2}");
        sb.AppendLine(inv, $"lavrador_10h dias={farmer} work={work} ({100.0 * work / Math.Max(1, farmer):F1}%) buy={buy} ({100.0 * buy / Math.Max(1, farmer):F1}%)");
        return sb.ToString();
    }
}
