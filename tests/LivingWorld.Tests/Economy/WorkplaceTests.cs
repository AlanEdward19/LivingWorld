using LivingWorld.Domain;

namespace LivingWorld.Tests.Economy;

/// <summary>Fase 5, T4: <see cref="Workplace"/> — vagas finitas (ECON-20), capacidade/perda de
/// estoque (ECON-02), estoque nunca negativo.</summary>
public class WorkplaceTests
{
    private static readonly ResourceType Wheat = new(1);
    private static readonly LocationType Farm = new(1);

    private static Workplace MakeWorkplace(int maxVacancies = 1, IReadOnlyList<NpcId>? employees = null) =>
        new(new WorkplaceId(1), Farm, new CellCoord(0, 0), maxVacancies,
            employees ?? [], new Dictionary<ResourceType, long>(), Money.Zero, new Dictionary<ResourceType, long>());

    private static EconomyRules RulesWithCapacity(long? capacity = null) => EconomyRules.Create(
        enabled: true, foodResourceId: 1, waterResourceId: 2,
        capacityByResourceLocation: capacity is { } c
            ? new Dictionary<(int, int), long> { [(Wheat.Id, Farm.Id)] = c }
            : new Dictionary<(int, int), long>(),
        spoilagePerDayByResource: new Dictionary<int, double>(),
        wageByProfession: new Dictionary<int, long>(),
        priceFloor: new Dictionary<int, long>(),
        priceCeiling: new Dictionary<int, long>(),
        priceSensitivity: 0.5,
        demandBaselinePerNpc: new Dictionary<int, double>()).Value!;

    [Fact]
    public void Hire_fails_when_employees_count_reaches_max_vacancies()
    {
        var workplace = MakeWorkplace(maxVacancies: 1, employees: [new NpcId(1)]);

        var result = workplace.Hire(new NpcId(2));

        Assert.False(result.IsSuccess);
        Assert.Single(workplace.Employees);
    }

    [Fact]
    public void Hire_succeeds_when_a_vacancy_is_open()
    {
        var workplace = MakeWorkplace(maxVacancies: 2, employees: [new NpcId(1)]);

        var result = workplace.Hire(new NpcId(2));

        Assert.True(result.IsSuccess);
        Assert.Contains(new NpcId(2), workplace.Employees);
    }

    [Fact]
    public void Deposit_under_capacity_reports_zero_loss()
    {
        var workplace = MakeWorkplace();
        var rules = RulesWithCapacity(capacity: 100);

        long lost = workplace.Deposit(Wheat, amount: 40, rules);

        Assert.Equal(0, lost);
        Assert.Equal(40, workplace.Stock[Wheat]);
    }

    [Fact]
    public void Deposit_over_capacity_reports_the_lost_amount()
    {
        var workplace = MakeWorkplace();
        var rules = RulesWithCapacity(capacity: 50);

        long lost = workplace.Deposit(Wheat, amount: 80, rules);

        Assert.Equal(30, lost);
        Assert.Equal(50, workplace.Stock[Wheat]);
    }

    [Fact]
    public void Withdraw_insufficient_fails_and_does_not_mutate_stock()
    {
        var workplace = MakeWorkplace();
        var rules = RulesWithCapacity(capacity: 100);
        workplace.Deposit(Wheat, amount: 10, rules);

        var result = workplace.Withdraw(Wheat, amount: 20);

        Assert.False(result.IsSuccess);
        Assert.Equal(10, workplace.Stock[Wheat]);
    }

    [Fact]
    public void Withdraw_sufficient_succeeds_and_decreases_stock()
    {
        var workplace = MakeWorkplace();
        var rules = RulesWithCapacity(capacity: 100);
        workplace.Deposit(Wheat, amount: 10, rules);

        var result = workplace.Withdraw(Wheat, amount: 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(6, workplace.Stock[Wheat]);
    }
}
