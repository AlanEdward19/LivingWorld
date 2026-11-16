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
    /// <summary>Fome crítica por o que a pessoa fazia (para achar a causa): viajando para comer, outra viagem, trabalhando, comendo,
    /// dormindo, o resto; e as horas com fome urgente (déficit acima da urgência), mais sensíveis.</summary>
    public long HungerTravelToEat, HungerTravel, HungerWork, HungerEating, HungerSleeping, HungerOther, HungerUrgent, HungerEatingAway, HungerSleepingAway, HungerEatingEmpty;
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
            int hungerDeficit = 100 - npc.HungerAt(now);
            var homeOf = npc.Household is { } hh ? world.FindHousehold(hh) : null;
            bool away = homeOf is not null && !homeOf.IsHomeless && npc.CurrentLocation != homeOf.Location;
            bool hungry = hungerDeficit > Critical, thirsty = 100 - npc.ThirstAt(now) > Critical;
            if (hungerDeficit > world.NeedsRules.UrgencyThreshold) HungerUrgent++;
            if (hungry)
            {
                var act = npc.CurrentAction;
                if (act == ActionType.Travel && npc.TravelPurpose == ActionType.Eat) HungerTravelToEat++;
                else if (act == ActionType.Travel) HungerTravel++;
                else if (act == ActionType.Work) HungerWork++;
                else if (act == ActionType.Eat)
                {
                    HungerEating++;
                    if (away) HungerEatingAway++;
                    else if (homeOf is not null && homeOf.Stock.GetValueOrDefault(new LivingWorld.Domain.Geography.ResourceType(world.EconomyRules.FoodResourceId)) == 0) HungerEatingEmpty++;
                }
                else if (act == ActionType.Sleep) { HungerSleeping++; if (away) HungerSleepingAway++; }
                else HungerOther++;
            }
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
            sb.AppendLine(inv, $"aflicao seed={seed} pa={py:F0} fome_crit={t.HungerCrit / py:F2} sede_crit={t.ThirstCrit / py:F2} fome_crit_longe={t.HungerCritAway / py:F2} sede_crit_longe={t.ThirstCritAway / py:F2} dep_casa_crit={t.DependentCritAtHome / depPy:F2} trab_h_dia={t.AdultWorkHours * t.HoursPerDay / Math.Max(1, t.AdultHours):F3} mortes_fome={t.Starved} mortes_sede={t.Dehydrated} fome_longe={i.StarvedAway} sede_longe={i.DehydratedAway} comeu_carga={i.PackEaten} bebeu_carga={i.PackDrunk} volta_comer={i.HomeForMeal} longe_sem_nada={i.CriticalAwayFromPack} racao_longe={t.Purchases.FoodRationsAway} volta_comer_pa={i.HomeForMeal / py:F2} fome_urg={t.HungerUrgent / py:F2} fc_ida_comer={t.HungerTravelToEat / py:F2} fc_viagem={t.HungerTravel / py:F2} fc_trab={t.HungerWork / py:F2} fc_comendo={t.HungerEating / py:F2} fc_dormindo={t.HungerSleeping / py:F2} fc_outro={t.HungerOther / py:F2} fc_comendo_longe={t.HungerEatingAway / py:F2} fc_comendo_casa_vazia={t.HungerEatingEmpty / py:F2} fc_dormindo_longe={t.HungerSleepingAway / py:F2} comeu_antes={i.AteBeforeLeaving} bebeu_antes={i.DrankBeforeLeaving} volta_do_trab={i.HomeForMealFromWork} volta_de_local={i.HomeForMealFromOtherPlace} volta_com_sede={i.HomeForMealThirsty} volta_de_casa_alheia={i.HomeForMealFromOtherHome} volta_conversa={i.HomeForMealSocial} volta_ocio={i.HomeForMealIdle} volta_viajando={i.HomeForMealTravel}");
        }
        return sb.ToString();
    }
}
