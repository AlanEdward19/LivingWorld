using System.Globalization;
using System.Text;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida que aguenta o ruído (AD-157, backlog 08b item 86): em vez das mortes de fome (raras, e que divergem com a
/// trajetória), horas-pessoa com fome ou sede crítica (déficit acima de 80) por pessoa-ano, separadas em longe de casa e em casa,
/// e as de quem fica em casa (criança abaixo de 12 e velho de 65 ou mais). Amostra de hora em hora; só lê o mundo.</summary>
public sealed class NeedDistressTally(WorldState world) : IWorldEventSink
{
    public const int Critical = 80;
    public long MeasureFromTick { get; init; }
    public double PersonHours, AdultHours, AdultWorkHours;
    public long HungerCrit, ThirstCrit, HungerCritAway, ThirstCritAway, DependentHours, DependentCritAtHome;
    public long Starved, Dehydrated;
    public int HoursPerDay => world.Calendar.HoursPerDay;
    public InventionTally Invention => world.InventionTally;
    public PurchaseTally Purchases => world.PurchaseTally;

    public void Record(WorldEvent evt)
    {
        if (evt.Tick < MeasureFromTick || evt.SourceSystem != YearEventTally.DeathSource) return;
        if (evt.Kind == WorldEventKind.Starvation) Starved++;
        else if (evt.Kind == WorldEventKind.Dehydration) Dehydrated++;
    }

    public void SampleHour()
    {
        long now = world.CurrentDate.TotalHours;
        if (now < MeasureFromTick) return;
        foreach (var npc in world.AliveNpcIndex.Alive)
        {
            PersonHours++;
            int age = npc.AgeYears(world.CurrentDate);
            if (age is >= 16 and <= 64)
            {
                AdultHours++;
                if (npc.CurrentAction == ActionType.Work) AdultWorkHours++;
            }
            bool hungry = 100 - npc.HungerAt(now) > Critical, thirsty = 100 - npc.ThirstAt(now) > Critical;
            var home = npc.Household is { } h ? world.FindHousehold(h) : null;
            bool atHome = home is null || home.IsHomeless || npc.CurrentLocation == home.Location;
            bool dependent = age < 12 || age >= 65;
            if (dependent && atHome) DependentHours++;
            if (!hungry && !thirsty) continue;
            if (hungry) { HungerCrit++; if (!atHome) HungerCritAway++; }
            if (thirsty) { ThirstCrit++; if (!atHome) ThirstCritAway++; }
            if (dependent && atHome) DependentCritAtHome++;
        }
    }

    /// <summary>Uma linha por seed (para parear a mesma seed em duas rodadas) e a soma; taxas em horas por pessoa-ano.</summary>
    public static string Summarize(IReadOnlyList<(ulong Seed, NeedDistressTally Tally)> runs, long hoursPerYear)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        foreach (var (seed, t) in runs)
        {
            double py = Math.Max(1e-9, t.PersonHours / hoursPerYear), depPy = Math.Max(1e-9, t.DependentHours / (double)hoursPerYear);
            var i = t.Invention;
            sb.AppendLine(inv, $"aflicao seed={seed} pa={py:F0} fome_crit={t.HungerCrit / py:F2} sede_crit={t.ThirstCrit / py:F2} fome_crit_longe={t.HungerCritAway / py:F2} sede_crit_longe={t.ThirstCritAway / py:F2} dep_casa_crit={t.DependentCritAtHome / depPy:F2} trab_h_dia={t.AdultWorkHours * t.HoursPerDay / Math.Max(1, t.AdultHours):F3} mortes_fome={t.Starved} mortes_sede={t.Dehydrated} fome_longe={i.StarvedAway} sede_longe={i.DehydratedAway} comeu_carga={i.PackEaten} bebeu_carga={i.PackDrunk} volta_comer={i.HomeForMeal} longe_sem_nada={i.CriticalAwayFromPack} racao_longe={t.Purchases.FoodRationsAway}");
        }
        return sb.ToString();
    }
}
