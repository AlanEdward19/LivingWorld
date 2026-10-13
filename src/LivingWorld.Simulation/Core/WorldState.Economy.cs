using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Population.Family;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — operações sobre locais de trabalho, processos, cultivos e dinheiro. O estado (campos e propriedades) fica em WorldState.cs.
public sealed partial class WorldState
{
    public void RecordResourceProduced(ResourceType resource, long amount) =>
        _resourceProduced[resource] = _resourceProduced.GetValueOrDefault(resource) + amount;

    public void RecordResourceConsumed(ResourceType resource, long amount) =>
        _resourceConsumed[resource] = _resourceConsumed.GetValueOrDefault(resource) + amount;

    /// <summary>Todo NPC já existiu, vivo ou morto — referência histórica não pode
    /// virar ponteiro solto (critério "nenhum evento após tick de morte referencia o NPC" exige
    /// que o NPC continue existindo para o sweep referencial provar isso).</summary>
    [Canonical] public IReadOnlyList<Npc> Npcs => _npcs;

    /// <summary>Households vivos — dissolvido (<see cref="Household.IsEmpty"/>) sai da lista.</summary>
    [Canonical] public IReadOnlyList<Household> Households => _households;

    [Canonical] public long NextNpcId => _nextNpcId;
    [Canonical] public long NextHouseholdId => _nextHouseholdId;

    internal WorkplaceId NextWorkplaceIdAndAdvance() => new(_nextWorkplaceId++);

    public void AddWorkplace(Workplace workplace)
    {
        _workplaces.Add(workplace);
        _workplaceById[workplace.Id] = workplace;
    }

    public Workplace? FindWorkplace(WorkplaceId id) => _workplaceById.GetValueOrDefault(id);

    internal ResourceProcessId NextResourceProcessIdAndAdvance() => new(_nextResourceProcessId++);

    public void AddResourceProcess(ResourceProcess process)
    {
        _resourceProcesses.Add(process);
        _nextResourceProcessId = Math.Max(_nextResourceProcessId, process.Id.Value + 1);
    }

    internal CropBatchId NextCropBatchIdAndAdvance() => new(_nextCropBatchId++);

    public void AddCropBatch(CropBatch crop)
    {
        _cropBatches.Add(crop);
        if (crop.Status != CropStatus.Harvested)
            _activeCropBatches.Add(crop);
        _nextCropBatchId = Math.Max(_nextCropBatchId, crop.Id.Value + 1);
    }

    /// <summary>Retira da lista canônica um lote já colhido (estado terminal sem leitores).</summary>
    internal void RetireHarvestedCropBatch(CropBatch crop)
    {
        if (crop.Status == CropStatus.Harvested) _cropBatches.Remove(crop);
    }

    public CropBatch? FindCropAt(CellCoord plot) =>
        ActiveCropBatches.FirstOrDefault(crop => crop.Plot == plot);

    /// <summary>Cunhagem explícita e rara — nunca chamada implicitamente por
    /// <see cref="MarketTransaction"/>/salário, só por um evento nomeado.</summary>
    public void Mint(TickContext ctx, Money amount, string reason)
    {
        _moneyMinted += amount;
        ctx.LogEvent(WorldEventKind.Minted, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
    }

    /// <summary>Retira massa da circulação (ex.: arquivo frio) — incrementa
    /// <see cref="MoneyDestroyed"/> sem o teto de <see cref="Destroy"/> (que só desfaz cunhagem).</summary>
    internal void BurnCirculatingMoney(TickContext ctx, Money amount, string reason)
    {
        if (amount.Amount <= 0) return;
        _moneyDestroyed += amount;
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
    }

    /// <summary>Destruição explícita e rara — falha (mesmo padrão de <see
    /// cref="Money.TryDebit"/>) se exceder a massa monetária líquida já cunhada
    /// (<see cref="MoneyMinted"/> - <see cref="MoneyDestroyed"/>), nunca altera o contador nesse
    /// caso.</summary>
    public Result<Unit> Destroy(TickContext ctx, Money amount, string reason)
    {
        var netSupply = _moneyMinted.Amount - _moneyDestroyed.Amount;
        if (amount.Amount > netSupply)
            return Result<Unit>.Fail("insufficient_money_supply");

        _moneyDestroyed += amount;
        ctx.LogEvent(WorldEventKind.Destroyed, $"{amount.Amount}|{reason}", sourceSystem: "WorldState");
        return Result<Unit>.Ok(Unit.Value);
    }

    // --- livro de trocas ---

    /// <summary>Janela, em dias, em que o livro guarda o detalhe diário antes de compactar em mês.</summary>
    public const int TradeLedgerDetailDays = 60;

    /// <summary>Soma uma compra ao agregado do dia da oficina e do recurso. Chamado só depois de a transação fechar:
    /// compra recusada não passa por aqui, então o livro e o hash ficam como estavam.</summary>
    public void RecordTrade(WorkplaceId workplace, ResourceType resource, long day, long quantity, long value)
    {
        int index = IndexOfTrade(workplace, resource.Id, day, monthly: false);
        if (index < 0)
        {
            // Lista ordenada: insere no lugar em vez de reordenar tudo a cada agregado novo.
            var created = new TradeAggregate(workplace, resource.Id, day, false, quantity, value, 1);
            int at = _tradeLedger.BinarySearch(created, Comparer<TradeAggregate>.Create(TradeOrder));
            _tradeLedger.Insert(at < 0 ? ~at : at, created);
        }
        else
        {
            var current = _tradeLedger[index];
            _tradeLedger[index] = current with
            {
                Quantity = current.Quantity + quantity,
                Value = current.Value + value,
                Purchases = current.Purchases + 1,
            };
        }

        CanonicalHashCache.MarkPropertyDirty(nameof(TradeLedger));
    }

    /// <summary>Dobra em mês os dias mais velhos que a janela de detalhe: o livro fica limitado (dias da janela mais um
    /// agregado por mês, oficina e recurso) em vez de crescer com o tempo.</summary>
    public void CompactTradeLedger(long today)
    {
        long cutoff = today - TradeLedgerDetailDays;
        int daysPerMonth = Calendar.DaysPerMonth;
        bool changed = false;
        for (int i = _tradeLedger.Count - 1; i >= 0; i--)
        {
            var entry = _tradeLedger[i];
            if (entry.Monthly || entry.Period > cutoff) continue;

            _tradeLedger.RemoveAt(i);
            long month = entry.Period / daysPerMonth;
            int target = IndexOfTrade(entry.Workplace, entry.ResourceId, month, monthly: true);
            if (target < 0)
                _tradeLedger.Add(new TradeAggregate(entry.Workplace, entry.ResourceId, month, true, entry.Quantity, entry.Value, entry.Purchases));
            else
            {
                var merged = _tradeLedger[target];
                _tradeLedger[target] = merged with
                {
                    Quantity = merged.Quantity + entry.Quantity,
                    Value = merged.Value + entry.Value,
                    Purchases = merged.Purchases + entry.Purchases,
                };
            }

            changed = true;
        }

        if (!changed) return;
        _tradeLedger.Sort(TradeOrder);
        CanonicalHashCache.MarkPropertyDirty(nameof(TradeLedger));
    }

    /// <summary>Agregados diários da oficina no intervalo de dias (inclusive), em ordem de dia e recurso. O que já foi
    /// compactado em mês não volta como dia.</summary>
    public IReadOnlyList<TradeAggregate> TradesOf(WorkplaceId workplace, long fromDay, long toDay) =>
        _tradeLedger.Where(e => e.Workplace == workplace && !e.Monthly && e.Period >= fromDay && e.Period <= toDay).ToList();

    private int IndexOfTrade(WorkplaceId workplace, int resourceId, long period, bool monthly)
    {
        for (int i = 0; i < _tradeLedger.Count; i++)
        {
            var e = _tradeLedger[i];
            if (e.Workplace == workplace && e.ResourceId == resourceId && e.Period == period && e.Monthly == monthly) return i;
        }

        return -1;
    }

    private static int TradeOrder(TradeAggregate a, TradeAggregate b)
    {
        int byKind = a.Monthly.CompareTo(b.Monthly);
        if (byKind != 0) return byKind;
        int byWorkplace = a.Workplace.Value.CompareTo(b.Workplace.Value);
        if (byWorkplace != 0) return byWorkplace;
        int byPeriod = a.Period.CompareTo(b.Period);
        return byPeriod != 0 ? byPeriod : a.ResourceId.CompareTo(b.ResourceId);
    }

    // --- livro do tesouro ---

    /// <summary>Soma ao livro da cidade no mês de <paramref name="tick"/> o que entrou e o que saiu do tesouro.</summary>
    public void RecordTreasury(CityId city, long tick, long revenue, long spending)
    {
        if (revenue == 0 && spending == 0) return;

        long month = tick / Calendar.HoursPerMonth;
        int index = _treasuryLedger.FindIndex(e => e.City == city && e.Month == month);
        if (index < 0)
        {
            var created = new TreasuryAggregate(city, month, revenue, spending);
            int at = _treasuryLedger.BinarySearch(created, Comparer<TreasuryAggregate>.Create(TreasuryOrder));
            _treasuryLedger.Insert(at < 0 ? ~at : at, created);
        }
        else
        {
            var current = _treasuryLedger[index];
            _treasuryLedger[index] = current with { Revenue = current.Revenue + revenue, Spending = current.Spending + spending };
        }

        CanonicalHashCache.MarkPropertyDirty(nameof(TreasuryLedger));
    }

    /// <summary>Meses da cidade no intervalo de dias (inclusive), em ordem de mês.</summary>
    public IReadOnlyList<TreasuryAggregate> TreasuryOf(CityId city, long fromDay, long toDay)
    {
        long daysPerMonth = Calendar.DaysPerMonth;
        return _treasuryLedger
            .Where(e => e.City == city && (e.Month + 1) * daysPerMonth > fromDay && e.Month * daysPerMonth <= toDay)
            .ToList();
    }

    private static int TreasuryOrder(TreasuryAggregate a, TreasuryAggregate b)
    {
        int byCity = a.City.Value.CompareTo(b.City.Value);
        return byCity != 0 ? byCity : a.Month.CompareTo(b.Month);
    }
}
