using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Cities;
using LivingWorld.Domain.Cities.Buildings;
using LivingWorld.Domain.Economy;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities.Queries;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Extraordinary.Mechanics;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Cities.Construction;

/// <summary>Avança a fila FIFO de construção de cada cidade: consome o
/// insumo da <see cref="City.Stock"/> proporcionalmente aos ticks já decorridos da receita —
/// pausa (nunca reverte progresso já pago) quando o insumo do tick não está disponível.</summary>
[LivingCapability("CITY_CONSTRUCTION")]
public sealed class ConstructionSystem : ISimulationSystem
{
    public const string SystemName = "cities-construction";

    public string Name => SystemName;
    public TickFrequency Frequency => TickFrequency.Midshift;

    public void Tick(WorldState world, TickContext ctx)
    {
        if (!world.CityRules.Enabled) return;

        foreach (var city in world.ActiveCities())
        {
            // FIFO entre projetos não-travados, mas um projeto já
            // pago (TicksRemaining == 0) cujo placement falhou por escassez de terra fica
            // "travado" na sua posição e é tentado de novo a custo zero todo tick, sem bloquear
            // quem vem depois dele -- só o PRIMEIRO projeto ainda não totalmente pago recebe o
            // orçamento de recursos deste tick (mesmo teto de "um projeto por cidade por tick" de
            // sempre, só mudou qual projeto se qualifica).
            foreach (var project in city.ConstructionQueue.ToList())
            {
                if (!world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)) continue;

                if (project.TicksRemaining == 0)
                {
                    // Travado (já pago, placement tinha falhado antes): retry de graça, sem
                    // consumir orçamento de recursos, e sem impedir os próximos projetos da fila
                    // de serem examinados neste mesmo tick.
                    if (CompleteProject(world, city, project, ctx))
                        city.RemoveConstructionProject(project);
                    continue;
                }

                // AD-179: com turma, a obra só anda pelas horas de quem trabalhou no lote (pagas por hora, SiteWork.Clock); sem a
                // regra, um tick da receita por dia (acelerado pela força de quem trabalha na cidade), como antes
                bool crew = NeedsCrew(world);
                if (crew) PrepareSite(world, ctx, city, project, recipe);
                // AD-180: com chuva forte, neve ou chão congelado a obra para no dia (a argamassa não pega, o barro não seca)
                bool workable = LivingWorld.Simulation.Climate.WeatherNow.OutdoorWorkable(world, city.Location);
                long workSteps = !workable ? 0 : crew ? SpendHaul(project) : ConstructionWorkSteps(world, city);
                for (long step = 0; step < workSteps && project.TicksRemaining > 0; step++)
                {
                    long tickIndex = recipe.TicksToBuild - project.TicksRemaining + 1;
                    var due = DueThisTick(recipe, project, tickIndex);

                    // Transacional: só consome se TODO recurso devido estiver disponível — insumo
                    // insuficiente por consumidor concorrente pausa a obra sem reverter o já pago
                    // (Edge Case da spec), nunca deixa a fila avançar parcialmente.
                    bool allAvailable = due.All(kv => city.Stock.GetValueOrDefault(kv.Key) >= kv.Value);
                    if (!allAvailable)
                        break;

                    foreach (var (resource, amount) in due)
                    {
                        city.WithdrawStock(resource, amount);
                        world.RecordResourceConsumed(resource, amount);
                        project.RecordConsumption(resource, amount);
                    }

                    project.Advance();
                    if (crew) project.HoursBanked--;
                    if (project.TicksRemaining == 0) project.Crew.Clear(); // pronta: a turma se desfaz (o prédio sobe ao achar terra)
                    if (project.TicksRemaining == 0 && CompleteProject(world, city, project, ctx))
                    {
                        city.RemoveConstructionProject(project);
                        break;
                    }
                }

                // Só um projeto consome (ou tenta consumir) o orçamento de recursos por cidade
                // por tick — pra a fila inteira, mesmo os travados retentados de graça acima.
                break;
            }
        }
    }

    /// <summary>AD-179: a fila só anda com turma quando a rua de verdade está ligada e a regra pede.</summary>
    internal static bool NeedsCrew(WorldState world) => world.StreetsOn && world.StreetRules.BuildNeedsCrew;

    /// <summary>AD-179: a obra da vez escolhe o lote (onde a turma trabalha), conta as viagens de material do estoque da cidade (o
    /// largo) até lá — ida carregada, volta vazia, pela rota — e refaz a turma do dia.</summary>
    private static void PrepareSite(WorldState world, TickContext ctx, City city, ConstructionProject project, BuildingRecipe recipe)
    {
        if (project.SiteX < 0)
        {
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, project.BuildingTypeId, ctx.CurrentTick);
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, CityPopulationQuery.Population(world, city.Id)).Bounds;
            var site = BuildingPlacementResolver.ResolveLot(candidate, city, world, bounds, chooseOrientation: true) is { } lot ? lot.Position : city.Location;
            project.SiteX = site.X;
            project.SiteY = site.Y;
            var rules = world.StreetRules;
            long units = recipe.Inputs.Values.Sum();
            long loads = (units + Math.Max(1, rules.BuildUnitsPerLoad) - 1) / Math.Max(1, rules.BuildUnitsPerLoad);
            double trip = LivingWorld.Simulation.Cities.Streets.StreetRoutes.CostOf(world, city.Location, site).Cost
                * LivingWorld.Simulation.Emergence.Mobility.LoadFactorOfKg(rules, rules.SiteLoadKg)
                + LivingWorld.Simulation.Cities.Streets.StreetRoutes.CostOf(world, site, city.Location).Cost;
            project.HaulHoursLeft = site == city.Location ? 0 : (long)Math.Ceiling(loads * trip);
        }
        LivingWorld.Simulation.Cities.Streets.SiteWork.Staff(world, ctx,
            new LivingWorld.Simulation.Cities.Streets.WorkSite(null, city, project));
        world.CanonicalHashCache.MarkPropertyDirty(nameof(WorldState.Cities));
    }

    /// <summary>AD-179: as horas da turma vão primeiro ao transporte do material; o que sobra vira passos da receita.</summary>
    private static long SpendHaul(ConstructionProject project)
    {
        long haul = Math.Min(project.HaulHoursLeft, project.HoursBanked);
        project.HaulHoursLeft -= haul;
        project.HoursBanked -= haul;
        return project.HoursBanked;
    }

    /// <returns>false somente quando um workplace-recipe não conseguiu resolver posição agora
    /// (escassez de terra) -- o chamador mantém o projeto na fila pra tentar de novo depois.</returns>
    private static bool CompleteProject(WorldState world, City city, ConstructionProject project, TickContext ctx)
    {
        if (!world.CityCatalog.BuildingRecipes.TryGetValue(project.BuildingTypeId, out var recipe)) return true;

        if (recipe.Workplace is { } workplaceRecipe)
        {
            // Resolve a posição ANTES de criar/adicionar o
            // Building -- com um candidato descartável (id só espiado via world.NextBuildingId,
            // nunca avançado se a resolução falhar) -- pra não deixar um Building órfão sem
            // Workplace acumulando a cada retry.
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, project.BuildingTypeId, ctx.CurrentTick);
            // Resolve precisa dos bounds atuais da cidade pra
            // tentar uma célula livre antes de cair no overflow;
            // ResolveGrownBounds já realimenta os boxes de overflow das próprias buildings pra
            // que os bounds cresçam de verdade.
            long population = CityPopulationQuery.Population(world, city.Id);
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
            // AD-173: a obra grava o lote escolhido (posição, porta para a rua, quintal), como toda construção
            if (BuildingPlacementResolver.ResolveLot(candidate, city, world, bounds, chooseOrientation: true) is not { } resolved) return false;

            var building = new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick,
                resolved.Position, resolved.Orientation, croftCells: resolved.Croft);
            world.AddBuilding(building);
            world.AddWorkplace(new Workplace(
                world.NextWorkplaceIdAndAdvance(), new LocationType(workplaceRecipe.LocationTypeId), resolved.Position,
                workplaceRecipe.MaxVacancies, employees: [], stock: new Dictionary<ResourceType, long>(),
                treasury: Money.Zero, prices: CopyPricesFromExisting(world, workplaceRecipe.LocationTypeId),
                city: city.Id, building: building.Id));
        }
        else
        {
            // AD-173: casa (ou outro prédio sem ofício) também nasce num lote gravado; sem terra, fica derivada como antes
            var candidate = new Building(new BuildingId(world.NextBuildingId), city.Id, project.BuildingTypeId, ctx.CurrentTick);
            long population = CityPopulationQuery.Population(world, city.Id);
            var bounds = CityOccupancy.ResolveGrownBounds(world, city, population).Bounds;
            var lot = BuildingPlacementResolver.ResolveLot(candidate, city, world, bounds, chooseOrientation: true);
            world.AddBuilding(lot is { } l
                ? new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick, l.Position, l.Orientation, croftCells: l.Croft)
                : new Building(world.NextBuildingIdAndAdvance(), city.Id, project.BuildingTypeId, ctx.CurrentTick));
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

    /// <summary>Portador com <c>attribute.strength</c> em Work na cidade acelera o
    /// consumo de insumo (passos inteiros; 1.0 = um tick de obra, como hoje).</summary>
    private static int ConstructionWorkSteps(WorldState world, City city)
    {
        double speed = 1;
        bool found = false;
        foreach (var npc in world.Npcs.OrderBy(item => item.Id.Value))
        {
            if (!npc.IsAlive || npc.City != city.Id || npc.CurrentAction != ActionType.Work)
                continue;
            double multiplier = AttributeMechanic.StrengthMultiplier(world, npc);
            speed = found ? Math.Max(speed, multiplier) : multiplier;
            found = true;
        }

        return Math.Max(1, (int)Math.Round(speed, MidpointRounding.AwayFromZero));
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

    /// <summary>Inicia uma obra — falha sem mutar nada quando a cidade não
    /// tem, agora, o insumo total da receita (Done-when 1); insumo é consumido ao longo dos
    /// ticks pelo <see cref="Tick"/>, não aqui.</summary>
    public static Result<Unit> StartConstruction(WorldState world, CityId cityId, int buildingTypeId)
    {
        var city = world.FindActiveCity(cityId);
        if (city is null) return Result<Unit>.Fail("City: não existe");
        if (!world.CityCatalog.BuildingRecipes.TryGetValue(buildingTypeId, out var recipe))
            return Result<Unit>.Fail("BuildingTypeId: receita não existe no catálogo");

        foreach (var (resource, amount) in recipe.Inputs)
            if (city.Stock.GetValueOrDefault(resource) < amount)
                return Result<Unit>.Fail($"Stock[{resource}]: insumo insuficiente para iniciar a obra");

        city.EnqueueConstruction(new ConstructionProject(
            city.Id, buildingTypeId, new Dictionary<ResourceType, long>(), recipe.TicksToBuild));
        return Result<Unit>.Ok(Unit.Value);
    }
}
