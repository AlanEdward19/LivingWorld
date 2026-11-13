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
        foreach (var workplace in world.Workplaces)
        {
            if (workplace.City != city || !world.EconomyCatalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe)) continue;
            if (recipe.Tool is not { } tool || tool.ResourceId != resourceId) continue;
            int workers = workplace.Employees.Count;
            book += Math.Max(0, tool.KitFor(workers) - workplace.Stock.GetValueOrDefault(new ResourceType(resourceId)))
                + (long)Math.Ceiling(workers * tool.WearPerWorkerYear / world.CurrentDate.Calendar.MonthsPerYear);
        }

        return book;
    }
}
