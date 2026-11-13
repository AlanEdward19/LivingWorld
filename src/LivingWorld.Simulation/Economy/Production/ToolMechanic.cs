using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Economy.Production;

/// <summary>Ferramenta de ofício (AD-144): o rendimento do trabalho cai sem ela, cada dia trabalhado a gasta, e quem a faz
/// trabalha por encomenda. Ofício sem <see cref="ProductionRecipe.Tool"/> não passa por aqui (fator 1, nada se gasta).</summary>
internal static class ToolMechanic
{
    public static double OutputFactor(Workplace workplace, ProductionRecipe recipe, int workers) =>
        recipe.Tool is { } tool ? tool.OutputFactor(workplace.Stock.GetValueOrDefault(new ResourceType(tool.ResourceId)), workers) : 1.0;

    /// <summary>Fator do local pelo catálogo (a lavoura usa a ferramenta da receita da fazenda).</summary>
    public static double OutputFactor(WorldState world, Workplace workplace, int workers) =>
        world.EconomyCatalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe) ? OutputFactor(workplace, recipe, workers) : 1.0;

    /// <summary>Desgaste de um dia de trabalho: <see cref="ToolUse.WearPerWorkerYear"/> ÷ dias do ano por trabalhador presente,
    /// arredondado sem viés por sorteio próprio. A ferramenta gasta sai do mundo (consumida), como a comida comida.</summary>
    public static void Wear(WorldState world, TickContext ctx, Workplace workplace, ProductionRecipe recipe, int workers)
    {
        if (recipe.Tool is not { } tool || workers <= 0 || tool.WearPerWorkerYear <= 0) return;
        var resource = new ResourceType(tool.ResourceId);
        long stock = workplace.Stock.GetValueOrDefault(resource);
        if (stock <= 0) return;

        var calendar = world.CurrentDate.Calendar;
        double expected = workers * tool.WearPerWorkerYear / (calendar.DaysPerMonth * calendar.MonthsPerYear);
        long worn = Math.Min(stock, CropSystem.RoundStochastic(expected, ctx, "tool-wear", workplace.Id.Value, ctx.CurrentTick));
        if (worn <= 0 || !workplace.Withdraw(resource, worn).IsSuccess) return;
        world.RecordResourceConsumed(resource, worn);
    }

    /// <summary>Encomendas da cidade para uma ferramenta: o que falta no jogo de cada cliente mais um mês do desgaste dele. É o
    /// estoque que o artesão mantém pronto; além disso ele não faz (não há comprador).</summary>
    public static long OrderBook(WorldState world, CityId city, int resourceId)
    {
        long book = 0;
        var workplaces = world.Workplaces;
        for (int i = 0; i < workplaces.Count; i++) // por índice: sem enumerador alocado (chamado em cada avaliação de emprego)
        {
            var workplace = workplaces[i];
            if (workplace.City != city || !world.EconomyCatalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe)) continue;
            if (recipe.Tool is not { } tool || tool.ResourceId != resourceId) continue;
            int workers = workplace.Employees.Count;
            book += Math.Max(0, tool.KitFor(workers) - workplace.Stock.GetValueOrDefault(new ResourceType(resourceId)))
                + (long)Math.Ceiling(workers * tool.WearPerWorkerYear / world.CurrentDate.Calendar.MonthsPerYear);
        }

        return book;
    }

    /// <summary>O artesão por encomenda só toma mais uma mão se a encomenda do dia passa do que a lotação atual faz (AD-144). O
    /// mestre não contrata quarenta ferreiros para o desgaste de semanas que um faz num dia; local que não faz ferramenta: sim.</summary>
    public static bool NeedsHands(WorldState world, Workplace workplace)
    {
        if (!world.EconomyCatalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe)) return true;
        bool makesTools = false;
        foreach (var (resourceId, perWorker) in recipe.Outputs)
        {
            if (!world.EconomyCatalog.IsTool(resourceId) || perWorker <= 0) continue;
            makesTools = true;
            double dailyOrders = (double)OrderBook(world, workplace.City, resourceId) / world.CurrentDate.Calendar.DaysPerMonth;
            if (dailyOrders > WorkingHands(world, workplace) * perWorker) return true;
        }

        return !makesTools;
    }

    /// <summary>Quem da folha ainda trabalha (adulto): o velho continua empregado mas não vai à oficina pela rotina. Contá-lo
    /// como mão fazia a forja de um ferreiro idoso não produzir nem contratar por décadas, e a fazenda ficava sem ferramenta.</summary>
    public static int WorkingHands(WorldState world, Workplace workplace)
    {
        int hands = 0;
        var employees = workplace.Employees;
        for (int i = 0; i < employees.Count; i++)
            if (world.FindNpc(employees[i]) is { IsAlive: true } npc
                && world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult)
                hands++;
        return hands;
    }

    /// <summary>A receita faz alguma ferramenta (trabalho por encomenda).</summary>
    public static bool MakesToOrder(WorldState world, ProductionRecipe recipe)
    {
        foreach (var resourceId in recipe.Outputs.Keys)
            if (world.EconomyCatalog.IsTool(resourceId)) return true;
        return false;
    }

    /// <summary>Os adultos vivos da folha, em ordem de id.</summary>
    public static IReadOnlyList<LivingWorld.Domain.Population.Npc> Hands(WorldState world, Workplace workplace)
    {
        var hands = new List<LivingWorld.Domain.Population.Npc>();
        foreach (var id in workplace.Employees.OrderBy(e => e.Value))
            if (world.FindNpc(id) is { IsAlive: true } npc
                && world.LifeStageRules.LifeStageOf(npc.AgeYears(world.CurrentDate)) == LivingWorld.Domain.Population.Body.LifeStage.Adult)
                hands.Add(npc);
        return hands;
    }
}
