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
        forge.Hire(world.Npcs.Last(n => n.IsAlive && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult).Id);
        double second = labor.ValueOf(forge);

        Assert.True(first > 1 && second == 0, $"{first} / {second}");
    }

    /// <summary>Histerese (AD-144): o ferreiro único vale o que ele mesmo faz, enquanto para quem está de fora a vaga não vale
    /// nada. Regressão da seed 7: o único se via sem valor, saía em 30 dias e outro moço entrava no lugar.</summary>
    [Fact]
    public void The_lone_smith_values_his_own_work_while_outsiders_see_no_opening()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        foreach (var npc in world.Npcs.Where(n => n.IsAlive).Take(10)) farm.Hire(npc.Id);
        var smith = world.Npcs.Last(n => n.IsAlive && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult);
        forge.Hire(smith.Id);
        var labor = LivingWorld.Simulation.Economy.Labor.LaborValueIndex.BuildForTick(world);

        Assert.True(labor.ValueOf(forge, smith) > 1 && labor.ValueOf(forge) == 0, $"{labor.ValueOf(forge, smith)} / {labor.ValueOf(forge)}");
    }

    /// <summary>Sucessão (AD-144): sem herdeiro, a oficina passa ao oficial mais hábil que trabalha nela.</summary>
    [Fact]
    public void Without_heirs_the_most_skilled_worker_of_the_forge_takes_it_over()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        var skill = ScenarioRunner.DefaultSkillsRules.SkillByProfession[2];
        var adults = world.Npcs.Where(n => n.IsAlive && n.AgeYears(world.CurrentDate) >= 20).Take(3).ToList();
        foreach (var npc in adults) { forge.Hire(npc.Id); npc.SwitchProfession(new LivingWorld.Domain.Population.ProfessionType(2)); }
        var expected = adults.OrderByDescending(n => n.Skills.Get(skill)).ThenBy(n => n.BirthDate.TotalHours).ThenBy(n => n.Id.Value).First();

        Assert.Equal(expected.Id, LivingWorld.Simulation.Population.Lifecycle.EstateSystem.Journeyman(world, forge)!.Id);
    }

    /// <summary>Regressão da seed 7: o ferreiro da folha faz a encomenda mesmo fora da forja na hora da produção (na falta,
    /// ele passava a hora na fila do mercado e a forja ficava dois anos sem fazer nada).</summary>
    [Fact]
    public void An_employed_smith_away_from_the_forge_still_makes_the_order()
    {
        var (world, _) = ScenarioRunner.Create(seed: 5, initialPopulation: 30);
        var farm = world.Workplaces.First(w => w.LocationType.Id == 1);
        var forge = world.Workplaces.First(w => w.LocationType.Id == 2);
        foreach (var npc in world.Npcs.Where(n => n.IsAlive).Take(10)) farm.Hire(npc.Id);
        var smith = world.Npcs.Last(n => n.IsAlive && n.CurrentLocation != forge.Location
            && world.LifeStageRules.LifeStageOf(n.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult);
        forge.Hire(smith.Id);

        new ProductionSystem(ScenarioRunner.DefaultSkillsRules).Tick(world, new LivingWorld.Simulation.Scheduling.TickContext(world, world.Rng, world.Scheduler));

        Assert.True(forge.Stock.GetValueOrDefault(Iron) > 0);
    }
}
