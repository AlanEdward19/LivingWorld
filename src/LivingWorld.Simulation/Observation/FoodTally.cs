using System.Globalization;
using System.Text;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da comida e da fauna de uma rodada (AD-135): amostra mensal de produção, consumo e estoque da vila,
/// contagem de mortes por fome e sede, furtos e ajudas por mês, e fauna por ano. Só lê o mundo: o hash não muda.</summary>
public sealed class FoodTally(WorldState world) : IWorldEventSink
{
    private long _lastProduced, _lastConsumed, _lastSown;
    private int _month;

    public List<long> ProducedByMonth { get; } = [];
    public List<long> ConsumedByMonth { get; } = [];
    public List<long> StockByMonth { get; } = [];
    public List<int> AliveByMonth { get; } = [];
    public List<int> FarmWorkersByMonth { get; } = [];
    public List<long> StarvationByMonth { get; } = [];
    public List<long> DehydrationByMonth { get; } = [];
    public List<long> TheftsByMonth { get; } = [];
    public List<long> AidByMonth { get; } = [];
    public List<long> AlmsByMonth { get; } = [];
    public List<int> ClimateByYear { get; } = [];
    public List<long> SeedSownByYear { get; } = [];
    public List<long> HarvestByYear { get; } = [];
    public List<(int Rabbits, int Wolves, int Plants)> EcologyByYear { get; } = [];

    private long _starv, _dehyd, _theft, _aid, _alms;

    public void Record(WorldEvent evt)
    {
        switch (evt.Kind)
        {
            case WorldEventKind.Starvation when evt.SourceSystem == YearEventTally.DeathSource: _starv++; return;
            case WorldEventKind.Dehydration when evt.SourceSystem == YearEventTally.DeathSource: _dehyd++; return;
            case WorldEventKind.TheftDiscovered: _theft++; return;
            case WorldEventKind.AidGiven:
                if (evt.Payload.StartsWith("city-", StringComparison.Ordinal)) _alms++; else _aid++;
                return;
        }
    }

    /// <summary>Chamado no fim de cada mês simulado.</summary>
    public void SampleMonth()
    {
        var food = new ResourceType(world.EconomyRules.FoodResourceId);
        long produced = world.ResourceProduced.GetValueOrDefault(food), consumed = world.ResourceConsumed.GetValueOrDefault(food);
        // a semente lançada entra como consumida (sai do estoque para a terra); aqui o consumo é o que se comeu
        long sown = world.CropBatches.Sum(c => c.LastSeedSown);
        // o mês que abre a semeadura começa uma safra nova (o contador do campo zera nele)
        bool seasonOpens = _month % world.Calendar.MonthsPerYear == world.AgricultureRules.SowStartMonth;
        long sownThisMonth = seasonOpens || sown < _lastSown ? sown : sown - _lastSown;
        _lastSown = sown;
        ProducedByMonth.Add(produced - _lastProduced);
        ConsumedByMonth.Add(consumed - _lastConsumed - sownThisMonth);
        _lastProduced = produced;
        _lastConsumed = consumed;
        long stock = 0;
        int farmWorkers = 0;
        foreach (var w in world.Workplaces)
        {
            stock += w.Stock.GetValueOrDefault(food);
            if (w.LocationType.Id == Economy.Production.CropSystem.DefaultFarmLocationTypeId) farmWorkers += w.Employees.Count;
        }
        foreach (var h in world.Households) stock += h.Stock.GetValueOrDefault(food);
        foreach (var c in world.CropBatches) stock += c.Barn + c.SeedReserve;
        StockByMonth.Add(stock);
        AliveByMonth.Add(world.AliveNpcIndex.Alive.Count);
        FarmWorkersByMonth.Add(farmWorkers);
        StarvationByMonth.Add(_starv); DehydrationByMonth.Add(_dehyd); TheftsByMonth.Add(_theft);
        AidByMonth.Add(_aid); AlmsByMonth.Add(_alms);
        _starv = _dehyd = _theft = _aid = _alms = 0;
        _month++;
        if (_month % world.Calendar.MonthsPerYear == 0) SampleYear();
    }

    private void SampleYear()
    {
        long year = world.ClimateYearOf(world.CurrentDate.TotalHours - 1);
        var farm = world.Workplaces.FirstOrDefault(w => w.LocationType.Id == Economy.Production.CropSystem.DefaultFarmLocationTypeId);
        ClimateByYear.Add(farm is null ? 1000 : world.ClimateFactorMilli(world.Map.RegionOf(farm.Location), year));
        long sown = 0, harvest = 0;
        foreach (var c in world.CropBatches) { sown += c.CropSeed; harvest += c.LastHarvest; }
        SeedSownByYear.Add(sown);
        HarvestByYear.Add(harvest);
        int rabbits = 0, wolves = 0;
        foreach (var a in world.Fauna)
        {
            if (!a.IsAlive) continue;
            if (a.Species == "rabbit") rabbits++; else if (a.Species == "wolf") wolves++;
        }
        EcologyByYear.Add((rabbits, wolves, world.Flora.Count));
    }

    /// <summary>Resumo de N seeds: ano típico mês a mês (depois do aquecimento), rendimento, anos de quebra, mortes de fome
    /// e sede por década e fauna por década.</summary>
    public static string Summarize(IReadOnlyList<FoodTally> runs, int warmupYears, int monthsPerYear)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        var prod = new double[monthsPerYear]; var cons = new double[monthsPerYear]; var stock = new double[monthsPerYear];
        var starvM = new double[monthsPerYear];
        double n = 0, personMonths = 0, consumedTotal = 0, workerYears = 0, producedTotal = 0;
        foreach (var r in runs)
            for (int m = warmupYears * monthsPerYear; m < r.ProducedByMonth.Count; m++)
            {
                int k = m % monthsPerYear;
                prod[k] += r.ProducedByMonth[m]; cons[k] += r.ConsumedByMonth[m]; stock[k] += r.StockByMonth[m];
                starvM[k] += r.StarvationByMonth[m];
                if (k == 0) n++;
                personMonths += r.AliveByMonth[m]; consumedTotal += r.ConsumedByMonth[m]; producedTotal += r.ProducedByMonth[m];
                workerYears += r.FarmWorkersByMonth[m] / (double)monthsPerYear;
            }
        n = Math.Max(1, n);
        sb.AppendLine(inv, $"comida ano tipico (media por seed-ano, meses 0..{monthsPerYear - 1}):");
        sb.AppendLine(inv, $"  producao={string.Join(",", prod.Select(v => (v / n).ToString("F0", inv)))}");
        sb.AppendLine(inv, $"  consumo={string.Join(",", cons.Select(v => (v / n).ToString("F0", inv)))}");
        sb.AppendLine(inv, $"  estoque_fim_mes={string.Join(",", stock.Select(v => (v / n).ToString("F0", inv)))}");
        sb.AppendLine(inv, $"  mortes_fome_por_mes_do_ano={string.Join(",", starvM.Select(v => v.ToString("F0", inv)))}");
        sb.AppendLine(inv, $"consumo_por_pessoa_ano={consumedTotal / Math.Max(1, personMonths) * monthsPerYear:F0} producao/consumo={producedTotal / Math.Max(1, consumedTotal):F3} producao_por_lavrador_ano={producedTotal / Math.Max(1, workerYears):F0}");
        double sown = 0, harvested = 0;
        foreach (var r in runs)
            for (int y = warmupYears; y < r.SeedSownByYear.Count; y++) { sown += r.SeedSownByYear[y]; harvested += r.HarvestByYear[y]; }
        sb.AppendLine(inv, $"rendimento_por_semente={(sown > 0 ? harvested / sown : 0):F2} (colhido={harvested:F0} semeado={sown:F0})");

        // anos de quebra: fator de clima abaixo do limiar de seca; o que aconteceu do mês da colheita até 12 meses depois
        int crisis = 0, normal = 0;
        double cDeaths = 0, nDeaths = 0, cTheft = 0, nTheft = 0, cAid = 0, nAid = 0, cAlms = 0, nAlms = 0;
        foreach (var r in runs)
            for (int y = warmupYears; y < r.ClimateByYear.Count; y++)
            {
                bool bad = r.ClimateByYear[y] < Domain.Climate.ClimateRules.DroughtBelowMilli;
                double d = 0, t = 0, a = 0, al = 0;
                for (int m = y * monthsPerYear + 6; m < Math.Min(r.StarvationByMonth.Count, (y + 1) * monthsPerYear + 6); m++)
                { d += r.StarvationByMonth[m]; t += r.TheftsByMonth[m]; a += r.AidByMonth[m]; al += r.AlmsByMonth[m]; }
                if (bad) { crisis++; cDeaths += d; cTheft += t; cAid += a; cAlms += al; }
                else { normal++; nDeaths += d; nTheft += t; nAid += a; nAlms += al; }
            }
        sb.AppendLine(inv, $"anos_de_quebra={crisis} em {crisis + normal} seed-anos; por ano de quebra: fome={cDeaths / Math.Max(1, crisis):F2} furtos={cTheft / Math.Max(1, crisis):F2} ajudas={cAid / Math.Max(1, crisis):F1} esmolas={cAlms / Math.Max(1, crisis):F1}; por ano normal: fome={nDeaths / Math.Max(1, normal):F2} furtos={nTheft / Math.Max(1, normal):F2} ajudas={nAid / Math.Max(1, normal):F1} esmolas={nAlms / Math.Max(1, normal):F1}");

        int decades = runs.Max(r => r.StarvationByMonth.Count) / (10 * monthsPerYear) + 1;
        var fome = new double[decades]; var sede = new double[decades];
        var rab = new double[decades]; var wol = new double[decades]; var pla = new double[decades]; var cnt = new double[decades];
        foreach (var r in runs)
        {
            for (int m = 0; m < r.StarvationByMonth.Count; m++)
            { fome[m / (10 * monthsPerYear)] += r.StarvationByMonth[m]; sede[m / (10 * monthsPerYear)] += r.DehydrationByMonth[m]; }
            for (int y = 0; y < r.EcologyByYear.Count; y++)
            { var e = r.EcologyByYear[y]; rab[y / 10] += e.Rabbits; wol[y / 10] += e.Wolves; pla[y / 10] += e.Plants; cnt[y / 10]++; }
        }
        sb.AppendLine(inv, $"mortes_fome_por_decada(soma seeds)={string.Join(",", fome.Select(v => v.ToString("F0", inv)))}");
        sb.AppendLine(inv, $"mortes_sede_por_decada(soma seeds)={string.Join(",", sede.Select(v => v.ToString("F0", inv)))}");
        sb.AppendLine(inv, $"coelhos_por_decada={string.Join(",", rab.Zip(cnt, (v, c) => (c > 0 ? v / c : 0).ToString("F0", inv)))}");
        sb.AppendLine(inv, $"lobos_por_decada={string.Join(",", wol.Zip(cnt, (v, c) => (c > 0 ? v / c : 0).ToString("F0", inv)))}");
        sb.AppendLine(inv, $"plantas_por_decada={string.Join(",", pla.Zip(cnt, (v, c) => (c > 0 ? v / c : 0).ToString("F0", inv)))}");
        return sb.ToString();
    }
}
