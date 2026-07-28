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
                world.AddBuilding(new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick));
                city.DequeueCompletedConstruction();
            }
        }
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
