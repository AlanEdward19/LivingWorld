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
}
