using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Simulation.Economy.Production;
using LivingWorld.Simulation.Scenarios;

namespace LivingWorld.Tests.Unit.Economy;

/// <summary>AD-144: a fazenda usa ferramenta de ferro que se gasta; a ferraria tem comprador e trabalha por encomenda.</summary>
public sealed class ToolDemandTests
{
    private const long OneMonth = 30 * 24;
    private static readonly ResourceType Iron = new(4);

    private sealed record Observed(long FarmTools, long FarmKit, long ForgeStock, long OrderBook, long ForgeCash, long IronWorn);

    private static Observed AfterTwoMonths(EconomyCatalog? catalog)
    {
        var (world, clock) = ScenarioRunner.Create(seed: 7, economyCatalog: catalog,
            economyRules: ScenarioRunner.DefaultEconomyRules with { SurplusDrawShare = 0 });
        clock.Run(world, 2 * OneMonth + 1);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        return new Observed(farm.Stock.GetValueOrDefault(Iron), ScenarioRunner.DefaultFarmTool.KitFor(farm.Employees.Count),
            forge.Stock.GetValueOrDefault(Iron), ToolMechanic.OrderBook(world, forge.City, 4), forge.Treasury.Amount,
            world.ResourceConsumed.GetValueOrDefault(Iron));
    }

    private static EconomyCatalog WithoutTools()
    {
        var recipes = ScenarioRunner.DefaultEconomyCatalog.Recipes.ToDictionary(kv => kv.Key, kv => kv.Value with { Tool = null });
        return ScenarioRunner.DefaultEconomyCatalog with { Recipes = recipes };
    }

    private static readonly Lazy<Observed> Tooled = new(() => AfterTwoMonths(null));
    private static readonly Lazy<Observed> Untooled = new(() => AfterTwoMonths(WithoutTools()));

    [Fact]
    public void The_farm_keeps_a_kit_for_each_worker()
    {
        Assert.True(Tooled.Value.FarmTools >= Tooled.Value.FarmKit * 9 / 10 && Tooled.Value.FarmKit > 0, $"{Tooled.Value.FarmTools} de {Tooled.Value.FarmKit}");
    }

    [Fact]
    public void The_forge_earns_from_selling_tools()
    {
        Assert.True(Tooled.Value.ForgeCash > Untooled.Value.ForgeCash, $"{Tooled.Value.ForgeCash} vs {Untooled.Value.ForgeCash}");
    }

    [Fact]
    public void Working_wears_the_tools_out()
    {
        Assert.True(Tooled.Value.IronWorn > 0 && Untooled.Value.IronWorn == 0, $"{Tooled.Value.IronWorn} / {Untooled.Value.IronWorn}");
    }

    [Fact]
    public void The_forge_works_to_order_instead_of_filling_its_store()
    {
        // sem encomenda a forja enchia o depósito (1000); por encomenda guarda menos que o jogo da vila
        Assert.True(Tooled.Value.ForgeStock < Tooled.Value.FarmKit && Untooled.Value.ForgeStock >= 1000, $"{Tooled.Value.ForgeStock} / {Untooled.Value.ForgeStock}");
    }

    [Fact]
    public void Without_tools_work_yields_the_untooled_share_and_a_full_kit_yields_everything()
    {
        var tool = ScenarioRunner.DefaultFarmTool;
        Assert.Equal(tool.UntooledOutput, tool.OutputFactor(stock: 0, workers: 10), 9);
        Assert.Equal(1.0, tool.OutputFactor(stock: tool.KitFor(10), workers: 10), 9);
    }

    [Fact]
    public void Half_a_kit_yields_halfway()
    {
        var tool = new ToolUse(4, KitPerWorker: 2, WearPerWorkerYear: 1, UntooledOutput: 0.5);
        Assert.Equal(0.75, tool.OutputFactor(stock: 10, workers: 10), 9);
    }

    [Theory]
    [InlineData(0, 1, 0.5)]
    [InlineData(2, -1, 0.5)]
    [InlineData(2, 1, 1.5)]
    public void An_invalid_tool_is_refused(double kit, double wear, double untooled)
    {
        Assert.False(ToolUse.Create(4, kit, wear, untooled).IsSuccess);
    }

    /// <summary>Todo bem que a vila padrão produz tem quem o use: comida e água (comer, beber), insumo de receita, ferramenta
    /// de ofício ou material de obra. Sem isso a oficina produz para ninguém (era o ferro antes da AD-144).</summary>
    [Fact]
    public void Every_good_the_default_village_produces_has_a_consumer()
    {
        var (world, _) = ScenarioRunner.Create(seed: 1, initialPopulation: 0);
        var consumed = new HashSet<int> { world.EconomyRules.FoodResourceId, world.EconomyRules.WaterResourceId };
        foreach (var recipe in world.EconomyCatalog.Recipes.Values)
        {
            consumed.UnionWith(recipe.Inputs.Keys);
            if (recipe.Tool is { } tool) consumed.Add(tool.ResourceId);
        }

        foreach (var building in world.CityCatalog.BuildingRecipes.Values) consumed.UnionWith(building.Inputs.Keys.Select(r => r.Id));

        var orphans = world.EconomyCatalog.Recipes.Values.SelectMany(r => r.Outputs.Keys).Where(r => !consumed.Contains(r)).Distinct();
        Assert.Empty(orphans);
    }

    /// <summary>Regressão (vila de 30 e seed 8): o trabalho na forja vale só a encomenda que a lotação atual não cobre. Sem encomenda (a
    /// fazenda ainda sem gente), não vale nada; antes valia saída × preço cheio e a vila inteira ia para a forja.</summary>
    [Fact]
    public void Forge_work_without_orders_is_worth_nothing()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);

        Assert.Equal(0, LivingWorld.Simulation.Economy.Labor.LaborValueIndex.BuildForTick(world).ValueOf(forge));
    }

    /// <summary>Regressão (vila de 30): a oficina vazia no fim do mês guarda a reserva da lotação possível (adultos da cidade
    /// ao salário do ofício), não a de um salário só; senão a retirada a deixava sem caixa para quem voltasse.</summary>
    [Fact]
    public void An_empty_farm_keeps_the_reserve_of_the_staff_it_can_take()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        long adults = world.Npcs.Count(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 15);

        Assert.Equal(adults * 90 * 12, LivingWorld.Simulation.Economy.Labor.Payroll.WorkingReserve(world, farm));
    }

    /// <summary>Produto marginal: com encomenda que um ferreiro dá conta, o primeiro vale a encomenda e o segundo, nada.</summary>
    [Fact]
    public void A_second_smith_adds_nothing_when_one_covers_the_orders()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        foreach (var npc in world.Npcs.Where(n => n.IsAlive).Take(10)) farm.Hire(npc.Id);
        var labor = LivingWorld.Simulation.Economy.Labor.LaborValueIndex.BuildForTick(world);
        double first = labor.ValueOf(forge);
        forge.Hire(world.Npcs.Last(n => n.IsAlive).Id);
        double second = labor.ValueOf(forge);

        Assert.True(first > 1 && second == 0, $"{first} / {second}");
    }
}
