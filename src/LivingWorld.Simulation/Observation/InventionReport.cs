using System.Globalization;
using System.Text;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Simulation.Observation;

/// <summary>Medida da invenção numa rodada (AD-141): contagem do sistema (tentativas, falhas por modo, invenções, ensino, cópia,
/// perda, viagens por água a pé e na embarcação) e o estado final (designs vivos, lápides, quem sabe, itens). Só lê o mundo.</summary>
public sealed record InventionSummary(
    ulong Seed, InventionTally Tally, int LiveDesigns, int Tombstones, int Knowers, int Items, IReadOnlyList<int> DesignIds, string FirstInvention,
    int CarryDesigns = 0, int WornCarryDesigns = 0, int WornCarryItems = 0, IReadOnlyList<int>? HaulDesignIds = null, int HaulItems = 0, string FirstHaul = "-")
{
    public static InventionSummary? Of(WorldState world)
    {
        if (world.Invention is not { } ledger) return null;
        var inv = CultureInfo.InvariantCulture;
        var first = ledger.Designs.Where(d => d.Origin == DesignOrigin.Invented).OrderBy(d => d.Tick).FirstOrDefault();
        string firstText = first is null ? "-" : string.Create(inv,
            $"npc {first.InventorId} ano {first.Tick / world.Calendar.HoursPerYear}: {Describe(first, world.InventionRules!)}");
        int knowers = ledger.Known.Select(k => k.NpcId).Distinct().Count(id => world.FindNpc(new NpcId(id)) is { IsAlive: true });
        var ids = ledger.Designs.Select(d => d.Id).Concat(ledger.Tombstones.Select(t => t.DesignId)).Distinct().Order().ToList();
        // AD-149: recipientes vivos e quantos se prendem ao corpo (tira), e as peças desses em uso
        var carry = ledger.Designs.Where(d => ledger.FunctionOf(d.Id) == CarryInvention.Function).ToList();
        var worn = carry.Where(d => PhysicsEvaluator.Contain(d.Parts, world.InventionRules!).Worn).Select(d => d.Id).ToHashSet();
        // AD-153: veículos de terra (vivos e lápides que foram) e o primeiro inventado
        var haul = ledger.HaulDesigns ?? [];
        var firstHaul = ledger.Designs.Where(d => d.Origin == DesignOrigin.Invented && ledger.FunctionOf(d.Id) == HaulInvention.Function).OrderBy(d => d.Tick).FirstOrDefault();
        string firstHaulText = firstHaul is null ? "-" : string.Create(inv,
            $"npc {firstHaul.InventorId} ano {firstHaul.Tick / world.Calendar.HoursPerYear}: {DescribeHaul(firstHaul, world.InventionRules!)}");
        return new InventionSummary(world.Seed, world.InventionTally, ledger.Designs.Count, ledger.Tombstones.Count, knowers, ledger.Items.Count, ids, firstText,
            carry.Count, worn.Count, ledger.Items.Count(i => worn.Contains(i.DesignId)), haul.ToList(),
            ledger.Items.Count(i => ledger.FunctionOf(i.DesignId) == HaulInvention.Function), firstHaulText);
    }

    /// <summary>Partes e o que a sonda de terra mediu (AD-153).</summary>
    public static string DescribeHaul(Design design, InventionRules rules)
    {
        var inv = CultureInfo.InvariantCulture;
        var h = PhysicsEvaluator.Haul(design.Parts, rules);
        var parts = design.Parts.Select(p => string.Create(inv, $"{p.Shape} {p.LengthMm}x{p.WidthMm}x{p.HeightMm} m{p.Material} {p.Joint}@z{p.OffsetZMm}"));
        return string.Create(inv,
            $"[{string.Join("; ", parts)}] modo={h.Mode} massa={h.MassG / 1000}kg carga={h.CapacityG / 1000}kg atrito={h.ResistancePm}‰ vazio={h.EmptyPullN}N horas={h.BuildMinutes / 60}");
    }

    /// <summary>Partes em texto curto (forma, medidas, material, parede, revestimento, junção) e o desempenho medido.</summary>
    public static string Describe(Design design, InventionRules rules)
    {
        var inv = CultureInfo.InvariantCulture;
        var perf = PhysicsEvaluator.Evaluate(design.Parts, rules, rules.ReferenceRiderKg);
        var parts = design.Parts.Select(p => string.Create(inv,
            $"{p.Shape} {p.LengthMm}x{p.WidthMm}x{p.HeightMm} m{p.Material}{(p.WallMm > 0 ? $" oco{p.WallMm}" : "")}{(p.Coating > 0 ? $" rev{p.Coating}" : "")} {p.Joint}@{p.OffsetYMm}"));
        return string.Create(inv,
            $"[{string.Join("; ", parts)}] massa={perf.MassG / 1000}kg carga={perf.PayloadG / 1000}kg calado={perf.DraftMm}mm GM={perf.MetacentricMm}mm v={perf.SpeedMmS}mm/s horas={perf.BuildMinutes / 60}");
    }

    public static string Summarize(IReadOnlyList<InventionSummary> runs)
    {
        if (runs.Count == 0) return "";
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        long Sum(Func<InventionTally, long> f) => runs.Sum(r => f(r.Tally));
        sb.AppendLine(inv, $"invencao seeds={runs.Count} ideias_descartadas={Sum(t => t.IdeasDiscarded)} prototipos={Sum(t => t.Attempts)} prototipos_falhos={Sum(t => t.PrototypeFailures)} (afundou={Sum(t => t.Sank)} virou={Sum(t => t.Capsized)} quebrou={Sum(t => t.Broke)} lento={Sum(t => t.TooSlow)} invalido={Sum(t => t.Invalid)})");
        sb.AppendLine(inv, $"invencao invencoes={Sum(t => t.Inventions)} seeds_com_invencao={runs.Count(r => r.Tally.Inventions > 0)} ensinados={Sum(t => t.Taught)} copiados={Sum(t => t.Copied)} engenharia_reversa={Sum(t => t.ReverseEngineered)} construidos={Sum(t => t.Built)} construcoes_falhas={Sum(t => t.BuildFailures)} perdidos={Sum(t => t.Lost)}");
        sb.AppendLine(inv, $"invencao final designs_vivos={runs.Sum(r => r.LiveDesigns)} lapides={runs.Sum(r => r.Tombstones)} quem_sabe={runs.Sum(r => r.Knowers)} itens={runs.Sum(r => r.Items)}");
        long foot = Sum(t => t.FootWaterTrips), craft = Sum(t => t.CraftWaterTrips);
        sb.AppendLine(inv, $"invencao viagens_por_agua a_pe={foot} na_embarcacao={craft} ({100.0 * craft / Math.Max(1, foot + craft):F1}%) horas_poupadas={Sum(t => t.HoursSavedX10) / 10.0:F0}");
        // AD-143
        sb.AppendLine(inv, $"invencao carga compras_cortadas_a_pe={Sum(t => t.FootCargoCapped)} unidades_cortadas={Sum(t => t.CargoUnitsCut)} idas_com_carga_da_peca={Sum(t => t.CraftCargoTrips)}");
        sb.AppendLine(inv, $"invencao mercado expostas={Sum(t => t.Listed)} vendas={Sum(t => t.Sales)} moedas={Sum(t => t.SaleCoins)} preco_medio={(double)Sum(t => t.SaleCoins) / Math.Max(1, Sum(t => t.Sales)):F1} reprecificacoes={Sum(t => t.Repriced)} falhas_de_quem_fazia_para_vender={Sum(t => t.SaleBuildFailures)}");
        sb.AppendLine(inv, $"invencao material kg_do_estoque={Sum(t => t.StockGramsUsed) / 1000} kg_do_mercado={Sum(t => t.MarketGramsBought) / 1000} kg_reaproveitados={Sum(t => t.SalvagedGrams) / 1000} horas_de_coleta_poupadas={Sum(t => t.GatherMinutesSaved) / 60}");
        sb.AppendLine(inv, $"invencao pesca meses_de_pesca={Sum(t => t.FishingMonths)} kg_de_peixe={Sum(t => t.FishGrams) / 1000} unidades_de_comida_da_agua={Sum(t => t.FishFoodUnits)}");
        sb.AppendLine(inv, $"invencao copia_por_evento vistas={Sum(t => t.Sightings)} copias_do_que_viu={Sum(t => t.CopiesFromSight)} lapides_com_relatos={Sum(t => t.TombstonesWithAccounts)} gatilhos_com_rumor={Sum(t => t.RumorBoosts)}");
        // AD-147
        sb.AppendLine(inv, $"invencao recipientes_vivos={runs.Sum(r => r.CarryDesigns)} com_tira={runs.Sum(r => r.WornCarryDesigns)} pecas_com_tira={runs.Sum(r => r.WornCarryItems)} seeds_com_tira={runs.Count(r => r.WornCarryDesigns > 0)}");
        sb.AppendLine(inv, $"invencao carregar seeds_com_recipiente={runs.Count(r => r.Tally.CarryInventions > 0)} invencoes={Sum(t => t.CarryInventions)} tentativas={Sum(t => t.CarryAttempts)} falhas={Sum(t => t.CarryPrototypeFailures)} descartes={Sum(t => t.CarryIdeasDiscarded)} copias={Sum(t => t.CarryCopies)} ensinados={Sum(t => t.CarryTaught)} encomendas={Sum(t => t.CarryBuilt)} vendas={Sum(t => t.CarrySales)} frustracoes_carga={Sum(t => t.CargoFrustrations)} frustracoes_longe={Sum(t => t.AwayFrustrations)}");
        sb.AppendLine(inv, $"invencao levar saidas_com_carga={Sum(t => t.PackTrips)} unidades_levadas={Sum(t => t.PackUnits)} comidas={Sum(t => t.PackEaten)} bebidas={Sum(t => t.PackDrunk)} devolvidas={Sum(t => t.PackReturned)} voltas_para_comer={Sum(t => t.HomeForMeal)} comer_longe_sem_nada={Sum(t => t.CriticalAwayFromPack)} idas_com_recipiente={Sum(t => t.CarrierCargoTrips)} kg_a_mais_por_recipiente={Sum(t => t.CarrierCargoGrams) / 1000} mortes_fome_longe={Sum(t => t.StarvedAway)} mortes_sede_longe={Sum(t => t.DehydratedAway)}");
        // AD-153
        sb.AppendLine(inv, $"invencao terra seeds_com_veiculo={runs.Count(r => r.Tally.HaulInventions > 0)} invencoes={Sum(t => t.HaulInventions)} prototipos={Sum(t => t.HaulAttempts)} falhas={Sum(t => t.HaulPrototypeFailures)} ensinados={Sum(t => t.HaulTaught)} encomendas={Sum(t => t.HaulBuilt)} vendas={Sum(t => t.HaulSales)} designs_vivos_ou_idos={runs.Sum(r => r.HaulDesignIds?.Count ?? 0)} pecas={runs.Sum(r => r.HaulItems)}");
        sb.AppendLine(inv, $"invencao terra compras_cortadas_por_terra={Sum(t => t.LandCargoCapped)} kg_cortados_por_terra={Sum(t => t.LandCargoGramsCut) / 1000} idas_com_veiculo={Sum(t => t.HaulTrips)} kg_alem_do_corpo={Sum(t => t.HaulGramsBeyondBody) / 1000} (arrasto={Sum(t => t.HaulDrag)} patim={Sum(t => t.HaulSkid)} roda={Sum(t => t.HaulRoll)})");
        double hOverlap = 0; int hPairs = 0;
        for (int i = 0; i < runs.Count; i++)
            for (int j = i + 1; j < runs.Count; j++)
            {
                var a = (runs[i].HaulDesignIds ?? []).ToHashSet();
                var b = runs[j].HaulDesignIds ?? [];
                if (a.Count == 0 || b.Count == 0) continue;
                hOverlap += (double)b.Count(a.Contains) / Math.Min(a.Count, b.Count);
                hPairs++;
            }
        sb.AppendLine(inv, $"invencao terra sobreposicao_entre_seeds={(hPairs == 0 ? 0 : 100 * hOverlap / hPairs):F1}% (pares={hPairs})");
        foreach (var r in runs.Where(r => r.FirstHaul != "-").Take(3)) sb.AppendLine(inv, $"invencao terra primeira seed={r.Seed}: {r.FirstHaul}");
        double overlap = 0; int pairs = 0;
        for (int i = 0; i + 1 < runs.Count; i++)
        {
            var a = runs[i].DesignIds.ToHashSet();
            var b = runs[i + 1].DesignIds;
            if (a.Count == 0 || b.Count == 0) continue;
            overlap += (double)b.Count(a.Contains) / Math.Min(a.Count, b.Count);
            pairs++;
        }
        sb.AppendLine(inv, $"invencao sobreposicao_media_entre_seeds_vizinhas={(pairs == 0 ? 0 : 100 * overlap / pairs):F1}% (pares={pairs})");
        foreach (var r in runs.Where(r => r.FirstInvention != "-").Take(3)) sb.AppendLine(inv, $"invencao primeira seed={r.Seed}: {r.FirstInvention}");
        return sb.ToString();
    }
}
