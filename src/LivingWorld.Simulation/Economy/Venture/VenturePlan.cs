using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy.Labor;
using LivingWorld.Simulation.Economy.Production;

namespace LivingWorld.Simulation.Economy.Venture;

/// <summary>A conta de quem pensa em abrir (AD-152): o preço que cobraria (o do concorrente que já vende na cidade, menos
/// <see cref="VentureRules.UndercutShare"/> para ter freguesia; sem concorrente que trabalhe, o preço do ofício), quanto venderia
/// no mês (a encomenda da cidade, para ferramenta; a procura-base por pessoa, para bem de mercado; dividida entre ele e quem já
/// vende; nunca mais do que a oficina nova faz) e a folha que pagaria. A vila que só sustenta um ferreiro não chama o segundo
/// enquanto o primeiro trabalha. <see cref="Margin"/> = (venda − folha) ÷ venda, entre 0 e 1: é a oportunidade percebida.
/// Bem que ninguém compra não tem margem: ninguém abre o que não vende.</summary>
internal sealed record VenturePlan(double Margin, long MonthlySales, long MonthlyPayroll, IReadOnlyDictionary<ResourceType, long> Prices)
{
    public static VenturePlan For(WorldState world, VentureRules rules, VentureTrade trade, CityId city)
    {
        var economy = world.EconomyRules;
        var calendar = world.CurrentDate.Calendar;
        var operating = new List<Workplace>();
        Workplace? reference = null;
        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (workplace.LocationType.Id != trade.LocationTypeId) continue;
            reference ??= workplace;
            if (workplace.City == city && Operating(world, workplace)) operating.Add(workplace);
        }

        var prices = new Dictionary<ResourceType, long>(reference?.Prices ?? new Dictionary<ResourceType, long>());
        long sales = 0;
        long population = -1;
        foreach (var (resourceId, perWorker) in trade.Recipe.Outputs.OrderBy(o => o.Key))
        {
            if (perWorker <= 0) continue;
            var resource = new ResourceType(resourceId);
            long floor = Math.Max(1, economy.PriceFloor.GetValueOrDefault(resourceId, 1));
            long price = Math.Max(floor, prices.GetValueOrDefault(resource, floor));
            long rival = long.MaxValue;
            foreach (var other in operating)
                if (other.Prices.TryGetValue(resource, out var p) && p > 0) rival = Math.Min(rival, p);
            if (rival != long.MaxValue)
                price = Math.Max(floor, rival - Math.Max(1, (long)Math.Floor(rival * rules.UndercutShare)));
            prices[resource] = price;

            long units;
            if (world.EconomyCatalog.IsTool(resourceId)) units = ToolMechanic.OrderBook(world, city, resourceId);
            else if (economy.DemandBaselinePerNpc.TryGetValue(resourceId, out var perPerson))
            {
                if (population < 0) population = CityPopulationQuery.Population(world, city);
                units = (long)Math.Floor(perPerson * population * calendar.DaysPerMonth);
            }
            else units = 0;

            // a freguesia de quem já trabalha não vem toda: o recém-chegado conta com a parte dele entre os que vendem
            units = Math.Min(units / (operating.Count + 1), perWorker * Math.Max(1, rules.StartCrew) * calendar.DaysPerMonth);
            sales += units * price;
        }

        long payroll = Math.Max(1, rules.StartCrew) * trade.Wage;
        double margin = sales <= 0 ? 0 : Math.Clamp((sales - payroll) / (double)sales, 0, 1);
        return new VenturePlan(margin, sales, payroll, prices);
    }

    /// <summary>A oficina trabalha: tem um adulto na folha e a última folha fechou.</summary>
    public static bool Operating(WorldState world, Workplace workplace) =>
        !workplace.PayrollFailed && ToolMechanic.WorkingHands(world, workplace) > 0;

    /// <summary>Há na cidade oficina do ofício que paga a folha, tem vaga e precisa de mais uma mão (a pergunta da contratação).</summary>
    public static bool HasPaidVacancy(WorldState world, VentureTrade trade, CityId city)
    {
        foreach (var workplace in world.Workplaces)
        {
            if (workplace.City != city || workplace.LocationType.Id != trade.LocationTypeId) continue;
            if (workplace.Employees.Count >= workplace.MaxVacancies || !Payroll.CanPay(world, workplace, trade.Profession)) continue;
            if (ToolMechanic.NeedsHands(world, workplace) && CropSystem.LandNeedsHands(world, workplace)) return true;
        }

        return false;
    }
}
