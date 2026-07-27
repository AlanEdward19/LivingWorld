using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Converte trabalho + recipe em saída de estoque por <see cref="Workplace"/> (Fase 5,
/// ECON-06/07/08), <c>Daily</c> (AD-042). Respeita <see cref="EconomyRules.Enabled"/>
/// (ECON-05).</summary>
public sealed class ProductionSystem : ISimulationSystem
{
    public const string SystemName = "economy-production";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.EconomyRules.Enabled) return;

        var catalog = world.EconomyCatalog;
        var rules = world.EconomyRules;

        foreach (var workplace in world.Workplaces.OrderBy(w => w.Id.Value))
        {
            if (catalog.Recipes.TryGetValue(workplace.LocationType.Id, out var recipe))
                Produce(world, ctx, workplace, recipe, rules);

            // Spoilage é propriedade do estoque, não da recipe — roda em todo Workplace, mesmo
            // um sem produção declarada (mercado, guarda, etc).
            ApplySpoilage(workplace, rules, ctx);
        }
    }

    private static void Produce(WorldState world, TickContext ctx, Workplace workplace, ProductionRecipe recipe, EconomyRules rules)
    {
        int workersPresent = workplace.Employees.Count(id =>
            world.FindNpc(id) is { IsAlive: true } npc && npc.CurrentLocation == workplace.Location);
        if (workersPresent == 0) return; // ECON-07: sem trabalhador presente, produção 0

        if (recipe.RequiresCellResource is { } requiredResource
            && !world.Map.CellAt(workplace.Location).Resources.Any(r => r.Id == requiredResource))
            return; // ECON-08: recipe exige recurso de célula ausente, produção 0

        int effectiveWorkers = Math.Min(workersPresent, recipe.MaxWorkersPerCycle);

        // Escala pra baixo pelo insumo mais escasso — nunca debita além do que o próprio
        // estoque do Workplace tem.
        int scale = effectiveWorkers;
        foreach (var (resourceId, perWorker) in recipe.Inputs)
        {
            if (perWorker <= 0) continue;
            long available = workplace.Stock.GetValueOrDefault(new ResourceType(resourceId));
            scale = Math.Min(scale, (int)(available / perWorker));
        }
        if (scale <= 0 && recipe.Inputs.Count > 0) return;

        foreach (var (resourceId, perWorker) in recipe.Inputs)
            workplace.Withdraw(new ResourceType(resourceId), perWorker * scale);

        foreach (var (resourceId, perWorker) in recipe.Outputs)
        {
            var resource = new ResourceType(resourceId);
            long produced = perWorker * scale;
            world.RecordResourceProduced(resource, produced); // ECON-15: conta o bruto, antes do clamp de capacidade
            long lost = workplace.Deposit(resource, produced, rules);
            if (lost > 0)
                ctx.LogEvent(WorldEventKind.ResourceLost, $"{workplace.Id.Value}|{resourceId}|{lost}");
        }
    }

    private static void ApplySpoilage(Workplace workplace, EconomyRules rules, TickContext ctx)
    {
        foreach (var (resource, amount) in workplace.Stock.ToList())
        {
            if (!rules.SpoilagePerDayByResource.TryGetValue(resource.Id, out var rate) || rate <= 0) continue;
            long spoiled = (long)(amount * rate);
            if (spoiled <= 0) continue;

            workplace.Withdraw(resource, Math.Min(spoiled, amount));
            ctx.LogEvent(WorldEventKind.ResourceLost, $"{workplace.Id.Value}|{resource.Id}|{spoiled}");
        }
    }
}
