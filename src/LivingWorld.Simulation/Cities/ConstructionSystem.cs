using LivingWorld.Domain;

namespace LivingWorld.Simulation;

/// <summary>Avança a fila FIFO de construção de cada cidade (Fase 8, T10, CITY-03): consome o
/// insumo da <see cref="City.Stock"/> proporcionalmente aos ticks já decorridos da receita —
/// pausa (nunca reverte progresso já pago) quando o insumo do tick não está disponível.</summary>
public sealed class ConstructionSystem : ISimulationSystem
{
    public const string SystemName = "cities-construction";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Daily;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;

        foreach (var city in world.Cities)
        {
            // FIFO (Done-when 3): só a cabeça da fila avança por tick — nunca por ordem de
            // dicionário/hash.
            if (city.ConstructionQueue.Count == 0) continue;
            var project = city.ConstructionQueue[0];
            if (!world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)) continue;

            long tickIndex = recipe.TicksToBuild - project.TicksRemaining + 1;
            var due = DueThisTick(recipe, project, tickIndex);

            // Transacional: só consome se TODO recurso devido estiver disponível — insumo
            // insuficiente por consumidor concorrente pausa a obra sem reverter o já pago
            // (Edge Case da spec), nunca deixa a fila avançar parcialmente.
            bool allAvailable = due.All(kv => city.Stock.GetValueOrDefault(kv.Key) >= kv.Value);
            if (!allAvailable) continue;

            foreach (var (resource, amount) in due)
            {
                city.WithdrawStock(resource, amount);
                project.RecordConsumption(resource, amount);
            }

            project.Advance();
            if (project.TicksRemaining == 0)
            {
                // dynamic-city-growth, round-3 fix D (CITYGROW-02b): só desenfileira quando o
                // projeto realmente completou -- escassez de terra deixa o projeto na fila pra
                // tentar de novo num tick futuro (design.md, Error Handling Strategy), nunca
                // desaparece sem criar nada.
                if (CompleteProject(world, city, project, ctx))
                    city.DequeueCompletedConstruction();
            }
        }
    }

    /// <returns>false somente quando um workplace-recipe não conseguiu resolver posição agora
    /// (escassez de terra) -- o chamador mantém o projeto na fila pra tentar de novo depois.</returns>
    private static bool CompleteProject(WorldState world, City city, ConstructionProject project, TickContext ctx)
    {
        if (!world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)) return true;

        if (recipe.Workplace is { } workplaceRecipe)
        {
            // dynamic-city-growth, round-3 fix D: resolve a posição ANTES de criar/adicionar o
            // Building -- com um candidato descartável (id só espiado via world.NextBuildingId,
            // nunca avançado se a resolução falhar) -- pra não deixar um Building órfão sem
            // Workplace acumulando a cada retry.
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, project.BuildingTypeId, ctx.CurrentTick);
            // dynamic-city-growth, T3/T4b: Resolve agora precisa dos bounds atuais da cidade pra
            // tentar uma célula livre antes de cair no overflow (CITYGROW-01/02);
            // ResolveGrownBounds já realimenta os boxes de overflow das próprias buildings pra
            // que os bounds cresçam de verdade (CITYGROW-03/05).
            long population = CityPopulationQuery.Population(world, city.Id);
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
            if (BuildingPlacementResolver.Resolve(candidate, city, world, bounds) is not { } resolved) return false;

            var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick);
            world.AddBuilding(building);
            world.AddWorkplace(new Workplace(
                world.NextWorkplaceIdAndAdvance(), new LocationType(workplaceRecipe.LocationTypeId), resolved.Position,
                workplaceRecipe.MaxVacancies, employees: [], stock: new Dictionary<ResourceType, long>(),
                treasury: Money.Zero, prices: CopyPricesFromExisting(world, workplaceRecipe.LocationTypeId)));
        }
        else
        {
            world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick));
        }
        return true;
    }

    private static Dictionary<ResourceType, long> CopyPricesFromExisting(WorldState world, int locationTypeId)
    {
        var existing = world.Workplaces.FirstOrDefault(wp => wp.LocationType.Id == locationTypeId);
        return existing is null
            ? new Dictionary<ResourceType, long>()
            : new Dictionary<ResourceType, long>(existing.Prices);
    }

    private static Dictionary<ResourceType, long> DueThisTick(BuildingRecipe recipe, ConstructionProject project, long tickIndex)
    {
        var due = new Dictionary<ResourceType, long>();
        foreach (var (resource, total) in recipe.Inputs)
        {
            // Último tick força o total exato — absorve o resto da divisão inteira, garantindo
            // Consumed == receita ao concluir (Done-when 2).
            long targetCumulative = tickIndex >= recipe.TicksToBuild ? total : total * tickIndex / recipe.TicksToBuild;
            long amountDue = targetCumulative - project.Consumed.GetValueOrDefault(resource);
            if (amountDue > 0) due[resource] = amountDue;
        }
        return due;
    }

    /// <summary>Inicia uma obra (Fase 8, T10, CITY-03) — falha sem mutar nada quando a cidade não
    /// tem, agora, o insumo total da receita (Done-when 1); insumo é consumido ao longo dos
    /// ticks pelo <see cref="Tick"/>, não aqui.</summary>
    public static Result<Unit> StartConstruction(WorldState world, CityId cityId, int buildingTypeId)
    {
        var city = world.FindCity(cityId);
        if (city is null) return Result<Unit>.Fail("City: não existe");
        if (!world.CityCatalog.BuildingRecipes.TryGetValue(buildingTypeId, out var recipe))
            return Result<Unit>.Fail("BuildingTypeId: receita não existe no catálogo");

        foreach (var (resource, amount) in recipe.Inputs)
            if (city.Stock.GetValueOrDefault(resource) < amount)
                return Result<Unit>.Fail($"Stock[{resource}]: insumo insuficiente para iniciar a obra");

        city.EnqueueConstruction(new ConstructionProject(
            cityId, buildingTypeId, new Dictionary<ResourceType, long>(), recipe.TicksToBuild));
        return Result<Unit>.Ok(Unit.Value);
    }
}
