using System.Globalization;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Observation;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Workers;

/// <summary>Sonda do abastecimento das casas (AD-148): <c>provision-trace &lt;seed&gt; &lt;ano-inicial&gt; &lt;ano-final&gt;</c>. Roda até o
/// ano inicial de mês em mês e depois hora a hora, e imprime uma linha por ano: despensa das casas em dias de consumo, banca da
/// fazenda (estoque, preço), horas dos adultos por ação, lavradores às 10 h, idas ao mercado e por que trazem pouco
/// (<see cref="PurchaseTally"/>). Respeita <c>LW_PRESSURE</c>. Só lê o mundo.</summary>
public static class ProvisionTraceCommand
{
    private static readonly ActionType[] Tracked =
        [ActionType.Work, ActionType.Buy, ActionType.Eat, ActionType.Sleep, ActionType.Socialize, ActionType.Idle];

    public static int Run(ulong seed, int fromYear, int toYear)
    {
        var inv = CultureInfo.InvariantCulture;
        PurchaseTally.Diagnose = true;
        var (world, baseClock) = ScenarioRunner.Create(seed, familyRules: PressureSwitch.FamilyRules(), needsRules: ProvisionSwitch.NeedsRules(), inventionRules: ProvisionSwitch.InventionRules(),
            agricultureRules: AgricultureSwitch.AgricultureRules(), economyRules: AgricultureSwitch.EconomyRules());
        var food = new FoodTally(world);
        var clock = new WorldClock(baseClock.Systems, baseClock.MaxIterationsPerTick, food);
        var foodType = new ResourceType(world.EconomyRules.FoodResourceId);
        var waterType = new ResourceType(world.EconomyRules.WaterResourceId);
        int hpd = world.Calendar.HoursPerDay, dpm = world.Calendar.DaysPerMonth, mpy = world.Calendar.MonthsPerYear;
        int midshift = hpd * 10 / 24;
        for (int m = 0; m < fromYear * mpy && world.AliveNpcIndex.Alive.Count > 0; m++)
        {
            clock.Run(world, world.Calendar.HoursPerMonth);
            food.SampleMonth();
        }

        Console.WriteLine("ano vivos casas | despensa: dias_med p0 p<1d p<2d p<4d agua_dias | banca: estoque_med h_vazia h<10 preco | " +
            "h/adulto/dia: work buy viagem_compra viagem_trab viagem_outra eat sleep social idle | lavr10h: n work buy viagem outro | " +
            "idas/casa/dia util rotina pessoas_por_ida_casa | compra: tent nada semdin meta_ok cart_corta carga_corta banca_curta(estq) " +
            "compras un/compra quer/compra desp_un/membro | agua poco compras | carteira<preco adultos | fome sede");
        for (int y = fromYear; y < toYear && world.AliveNpcIndex.Alive.Count > 0; y++)
        {
            var before = world.PurchaseTally.Copy();
            var pantry = new List<double>();
            var pantryWater = new List<double>();
            double adultHours = 0, marketHours = 0, marketStock = 0, marketEmpty = 0, marketLow = 0, price = 0;
            var hours = new double[Tracked.Length];
            double travelBuy = 0, travelWork = 0, travelOther = 0;
            double farmN = 0, farmWork = 0, farmBuy = 0, farmTravel = 0, farmOther = 0;
            double hhDays = 0, buyerHh = 0, buyers = 0, poorAdults = 0, adultSamples = 0;
            int starvStart = food.StarvationByMonth.Count;
            var buyersToday = new HashSet<(long Household, long Npc)>();
            var farmerActs = new SortedDictionary<string, long>(StringComparer.Ordinal);
            long farmerUrgent = 0, farmerHungerUrgent = 0, farmerThirstUrgent = 0, farmerSleepUrgent = 0, farmerSocialUrgent = 0;
            for (int d = 0; d < mpy * dpm; d++)
            {
                buyersToday.Clear();
                for (int h = 0; h < hpd; h++)
                {
                    clock.Run(world, 1);
                    var farm = world.Workplaces.FirstOrDefault(w => w.LocationType.Id == CropSystem.DefaultFarmLocationTypeId);
                    long foodPrice = farm?.Prices.GetValueOrDefault(foodType) ?? 0;
                    if (farm is not null)
                    {
                        long s = farm.Stock.GetValueOrDefault(foodType);
                        marketHours++; marketStock += s; price += foodPrice;
                        if (s == 0) marketEmpty++;
                        if (s < 10) marketLow++;
                    }
                    int hour = world.CurrentDate.Hour;
                    foreach (var npc in world.AliveNpcIndex.Alive)
                    {
                        if (npc.AgeYears(world.CurrentDate) < 16) continue;
                        adultHours++;
                        var act = npc.CurrentAction;
                        if (act == ActionType.Travel)
                        {
                            if (npc.TravelPurpose == ActionType.Buy) travelBuy++;
                            else if (npc.TravelPurpose == ActionType.Work) travelWork++;
                            else travelOther++;
                        }
                        else
                            for (int i = 0; i < Tracked.Length; i++)
                                if (act == Tracked[i]) { hours[i]++; break; }
                        if ((act == ActionType.Buy || act == ActionType.Travel && npc.TravelPurpose == ActionType.Buy) && npc.Household is { } hid)
                            buyersToday.Add((hid.Value, npc.Id.Value));
                        if (hour == midshift)
                        {
                            adultSamples++;
                            if (npc.Wallet.Amount < foodPrice) poorAdults++;
                            if (farm is not null && npc.Employer == farm.Id)
                            {
                                farmN++;
                                long now = world.CurrentDate.TotalHours;
                                string key = act == ActionType.Travel ? "Travel>" + (npc.TravelPurpose?.ToString() ?? "-") : act?.ToString() ?? "-";
                                farmerActs[key] = farmerActs.GetValueOrDefault(key) + 1;
                                var nr = world.NeedsRules;
                                if (npc.HasUrgentNeed(nr, now)) farmerUrgent++;
                                if (100 - npc.HungerAt(now) > nr.UrgencyThreshold) farmerHungerUrgent++;
                                if (100 - npc.ThirstAt(now) > nr.UrgencyThreshold) farmerThirstUrgent++;
                                if (100 - npc.SleepAt(now) > nr.UrgencyThreshold) farmerSleepUrgent++;
                                if (100 - npc.SocialAt(now) > nr.UrgencyThreshold) farmerSocialUrgent++;
                                if (act == ActionType.Work && npc.CurrentLocation == farm.Location) farmWork++;
                                else if (act == ActionType.Buy) farmBuy++;
                                else if (act == ActionType.Travel) farmTravel++;
                                else farmOther++;
                            }
                        }
                    }
                }

                // fim do dia: despensa por membro e quem foi comprar
                foreach (var hh in world.Households)
                {
                    int members = hh.Members.Count;
                    if (members == 0) continue;
                    hhDays++;
                    pantry.Add((double)hh.Stock.GetValueOrDefault(foodType) / members);
                    pantryWater.Add((double)hh.Stock.GetValueOrDefault(waterType) / members);
                }
                var byHh = buyersToday.GroupBy(b => b.Household).ToList();
                buyerHh += byHh.Count;
                buyers += buyersToday.Count;
                if ((d + 1) % dpm == 0) food.SampleMonth();
            }

            var t = world.PurchaseTally;
            var b = before;
            int alive = world.AliveNpcIndex.Alive.Count;
            // consumo por pessoa-dia do ano (o que se comeu, sem a semente)
            double consumed = 0, personMonths = 0;
            for (int m = Math.Max(0, food.ConsumedByMonth.Count - mpy); m < food.ConsumedByMonth.Count; m++)
            { consumed += food.ConsumedByMonth[m]; personMonths += food.AliveByMonth[m]; }
            double perDay = personMonths > 0 ? consumed / (personMonths * dpm) : 1;
            double Days(double units) => perDay > 0 ? units / perDay : 0;
            pantry.Sort();
            double Share(Func<double, bool> f) => pantry.Count == 0 ? 0 : 100.0 * pantry.Count(f) / pantry.Count;
            long foodBuys = t.FoodBought - b.FoodBought;
            long foodTried = foodBuys + (t.FoodStockShort - b.FoodStockShort) + (t.FoodNoMoney - b.FoodNoMoney);
            double aDays = Math.Max(1, adultHours / hpd);
            long starv = 0, dehyd = 0;
            for (int m = starvStart; m < food.StarvationByMonth.Count; m++) { starv += food.StarvationByMonth[m]; dehyd += food.DehydrationByMonth[m]; }
            Console.WriteLine(string.Create(inv,
                $"{y} {alive} {world.Households.Count(h => h.Members.Count > 0)} | " +
                $"{Days(pantry.Count > 0 ? pantry.Average() : 0):F2} {Share(v => v < 1):F0}% {Share(v => Days(v) < 1):F0}% {Share(v => Days(v) < 2):F0}% {Share(v => Days(v) < 4):F0}% {(pantryWater.Count > 0 ? pantryWater.Average() : 0):F1} | " +
                $"{marketStock / Math.Max(1, marketHours):F0} {100 * marketEmpty / Math.Max(1, marketHours):F0}% {100 * marketLow / Math.Max(1, marketHours):F0}% {price / Math.Max(1, marketHours):F1} | " +
                $"{hours[0] / aDays:F2} {hours[1] / aDays:F2} {travelBuy / aDays:F2} {travelWork / aDays:F2} {travelOther / aDays:F2} {hours[2] / aDays:F2} {hours[3] / aDays:F2} {hours[4] / aDays:F2} {hours[5] / aDays:F2} | " +
                $"{farmN / (mpy * dpm):F1} {100 * farmWork / Math.Max(1, farmN):F0}% {100 * farmBuy / Math.Max(1, farmN):F0}% {100 * farmTravel / Math.Max(1, farmN):F0}% {100 * farmOther / Math.Max(1, farmN):F0}% | " +
                $"{(t.BuyByUtility - b.BuyByUtility + t.BuyByRestock - b.BuyByRestock) / Math.Max(1, hhDays):F2} {t.BuyByUtility - b.BuyByUtility} {t.BuyByRestock - b.BuyByRestock} {buyers / Math.Max(1, buyerHh):F2} | " +
                $"{t.Attempts - b.Attempts} {t.AttemptsNothingBought - b.AttemptsNothingBought} {t.FoodNoMoney - b.FoodNoMoney} {t.FoodTargetMet - b.FoodTargetMet} {t.FoodWalletCapped - b.FoodWalletCapped} {t.FoodCargoCut - b.FoodCargoCut} " +
                $"{t.FoodStockShort - b.FoodStockShort}({(t.MarketFoodAtShort - b.MarketFoodAtShort) / (double)Math.Max(1, t.FoodStockShort - b.FoodStockShort):F1}) " +
                $"{foodBuys} {(t.FoodUnits - b.FoodUnits) / (double)Math.Max(1, foodBuys):F1} {(t.FoodWantedUnits - b.FoodWantedUnits) / (double)Math.Max(1, foodTried):F1} " +
                $"{(t.FoodPantryUnitsBefore - b.FoodPantryUnitsBefore) / (double)Math.Max(1, t.FoodPantryMembers - b.FoodPantryMembers):F2} | " +
                $"{t.WaterFromWell - b.WaterFromWell} {t.WaterBought - b.WaterBought} | {100 * poorAdults / Math.Max(1, adultSamples):F0}% {adultSamples / (mpy * dpm):F0} | {starv} {dehyd} consumo/pessoa/dia={perDay:F2} racao_oficina comida={t.FoodRations - b.FoodRations} longe={t.FoodRationsAway - b.FoodRationsAway} agua={t.WaterRations - b.WaterRations}"));
            string acts = string.Join(" ", farmerActs.Select(kv => kv.Key + "=" + (100.0 * kv.Value / Math.Max(1, farmN)).ToString("F0", inv) + "%"));
            double F(long v) => 100.0 * v / Math.Max(1, farmN);
            {
                var a = world.PurchaseTally; var z = before;
                var urg = string.Join(" ", Enum.GetValues<ActionType>().Where(x => (int)x < 16 && a.UrgentAtWork[(int)x] - z.UrgentAtWork[(int)x] > 0)
                    .Select(x => x + "=" + (a.UrgentAtWork[(int)x] - z.UrgentAtWork[(int)x])));
                Console.WriteLine($"   hora_de_trabalho decisoes: urgentes[{urg}] nao_urgentes={a.NonUrgentAtWork - z.NonUrgentAtWork} repor={a.RestockAtWork - z.RestockAtWork} humor={a.MoodAtWork - z.MoodAtWork} percepcao={a.PerceptionAtWork - z.PerceptionAtWork} desenvolvimento={a.DevelopmentAtWork - z.DevelopmentAtWork} doente={a.SickAtWork - z.SickAtWork} cuidar={a.CareAtWork - z.CareAtWork}");
            }
            {
                int adults = 0, blocked = 0, blockedEmployed = 0, employed = 0; double minFine = 1;
                foreach (var npc in world.AliveNpcIndex.Alive)
                {
                    if (npc.AgeYears(world.CurrentDate) < 16) continue;
                    adults++;
                    if (npc.Employer is not null) employed++;
                    if (npc.Development is { } dev && dev.FineMotor < 1.0)
                    {
                        blocked++; minFine = Math.Min(minFine, dev.FineMotor);
                        if (npc.Employer is not null) blockedEmployed++;
                    }
                }
                Console.WriteLine(string.Create(inv, $"   adultos={adults} empregados={employed} motor_fino<1={blocked} ({100.0 * blocked / Math.Max(1, adults):F0}%) empregados_bloqueados={blockedEmployed} min={minFine:F2}"));
            }
            Console.WriteLine(string.Create(inv, $"   lavr10h acoes: {acts} urgente={F(farmerUrgent):F0}% fome={F(farmerHungerUrgent):F0}% sede={F(farmerThirstUrgent):F0}% sono={F(farmerSleepUrgent):F0}% social={F(farmerSocialUrgent):F0}%"));
        }

        return 0;
    }
}
