using System.Text.Json.Nodes;
using LivingWorld.Domain.Behavior;
using LivingWorld.Domain.Extraordinary;
using LivingWorld.Domain.Geography;
using LivingWorld.Domain.History;
using LivingWorld.Domain.Population;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Economy;
using LivingWorld.Simulation.Extraordinary.Engine;
using LivingWorld.Simulation.Extraordinary.Systems;
using LivingWorld.Simulation.Population;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Simulation.Authoring;

public sealed record PersonalityValues(
    int Extroversion, int Agreeableness, int Conscientiousness, int EmotionalStability,
    int Openness, int Ambition, int Loyalty, int Altruism, int Impulsivity, int RiskAversion);

/// <summary>Resposta de <c>POST /authoring/households</c>: o domicílio, a cidade e as pessoas criadas.</summary>
public sealed record AuthoredHouseholdCreated(long HouseholdId, string CityId, IReadOnlyList<long> NpcIds);

/// <summary>Resposta de <c>POST /authoring/workplaces</c>: o local, o edifício (se havia cidade) e onde ficou.</summary>
public sealed record AuthoredWorkplaceCreated(long WorkplaceId, long? BuildingId, string CityId, int X, int Y);

/// <summary>Borda autoritativa única para intervenções do operador. Todos os argumentos são
/// validados antes da primeira escrita e cada aceite entra no log causal do mundo.</summary>
public static class WorldAuthoringCommands
{
    public static Result<Unit> RewritePersonality(
        WorldState world, TickContext ctx, NpcId npcId, PersonalityValues values)
    {
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("NpcId: NPC ausente ou morto");
        var personality = Personality.Create(
            values.Extroversion, values.Agreeableness, values.Conscientiousness,
            values.EmotionalStability, values.Openness, values.Ambition, values.Loyalty,
            values.Altruism, values.Impulsivity, values.RiskAversion);
        if (!personality.IsSuccess) return Result<Unit>.Fail(personality.Error!);
        npc.RewritePersonality(personality.Value!);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"personality|{npcId.Value}", sourceSystem: "WorldAuthoringCommands");
        return Result<Unit>.Ok(Unit.Value);
    }

    public static Result<int> BreakRelationships(
        WorldState world, TickContext ctx, NpcId first, NpcId second)
    {
        if (first == second) return Result<int>.Fail("OtherNpcId: deve ser diferente");
        if (world.FindNpc(first) is null || world.FindNpc(second) is null)
            return Result<int>.Fail("NpcId: NPC ausente");
        int removed = world.RemoveRelationshipsBetween(first, second);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"relationships.break|{first.Value}|{second.Value}|{removed}", sourceSystem: "WorldAuthoringCommands");
        return Result<int>.Ok(removed);
    }

    /// <summary>Starter de cada recém-chegado, cunhado: o mesmo pecúlio que o cenário dá na criação (comida e água no domicílio, moedas na carteira).</summary>
    private const long NewcomerStock = 50;

    /// <summary>Cria uma família no mundo em andamento (#46 fatia 4, ADR-0033). O corpo é um item de <c>AuthoredHouseholds</c> do cenário,
    /// validado pelo mesmo loader; inválido não escreve nada.</summary>
    public static Result<AuthoredHouseholdCreated> CreateHousehold(WorldState world, TickContext ctx, JsonObject body)
    {
        var root = new JsonObject { ["AuthoredHouseholds"] = new JsonArray(JsonNode.Parse(body.ToJsonString())) };
        var loaded = AuthoredHouseholdsLoader.Load(
            root, world.PopulationCatalog, world.PopulationRules.LifeTable.MaxLongevityYears,
            world.Map.Settlements.Select(s => s.Name).ToList(), appearanceEnabled: world.AppearanceRules is not null);
        if (!loaded.IsSuccess) return Result<AuthoredHouseholdCreated>.Fail(loaded.Error!);
        var family = loaded.Value![0];

        var city = family.Settlement is null
            ? world.ActiveCities().OrderBy(c => c.FoundedAtTick).ThenBy(c => c.Id.Value).FirstOrDefault()
            : world.ActiveCities().FirstOrDefault(c => c.Location == world.Map.Settlements.First(s => s.Name == family.Settlement).Cell);
        if (city is null)
            return Result<AuthoredHouseholdCreated>.Fail($"AuthoredHouseholds.Settlement: '{family.Settlement ?? "inicial"}' não tem cidade no mundo");

        var residents = world.Npcs.Where(n => n.IsAlive && n.City == city.Id).ToList();
        var culture = (residents.Count > 0 ? residents : world.Npcs.ToList())
            .GroupBy(n => n.Culture).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Id)
            .Select(g => g.Key).FirstOrDefault(new CultureId(1));

        long firstNpc = world.NextNpcId, firstHousehold = world.NextHouseholdId;
        try
        {
            PopulationSeeder.SeedAuthored(world, family, culture, city.Location, city);
        }
        catch (InvalidOperationException ex)
        {
            return Result<AuthoredHouseholdCreated>.Fail($"AuthoredHouseholds: {ex.Message}");
        }

        var newcomers = world.Npcs.Where(n => n.Id.Value >= firstNpc).ToList();
        if (world.EconomyRules.Enabled)
        {
            var household = world.Households.First(h => h.Id.Value == firstHousehold);
            household.Deposit(new ResourceType(world.EconomyRules.FoodResourceId), NewcomerStock * newcomers.Count);
            household.Deposit(new ResourceType(world.EconomyRules.WaterResourceId), NewcomerStock * newcomers.Count);
            foreach (var npc in newcomers) npc.CreditWallet(new Money(NewcomerStock));
            world.Mint(ctx, new Money(NewcomerStock * newcomers.Count), "authored-immigration");
        }

        var ids = newcomers.Select(n => n.Id.Value).ToList();
        ctx.LogEvent(
            WorldEventKind.AuthoredImmigration, $"{firstHousehold}|{city.Id.Value}|{string.Join(',', ids)}",
            sourceSystem: "WorldAuthoringCommands");
        return Result<AuthoredHouseholdCreated>.Ok(new AuthoredHouseholdCreated(firstHousehold, city.Id.Value.ToString(), ids));
    }

    /// <summary>Funda um local de trabalho no mundo em andamento (#46 fatia 4, ADR-0033): item de <c>Workplaces</c> do cenário, mesma colocação.</summary>
    public static Result<AuthoredWorkplaceCreated> CreateWorkplace(WorldState world, TickContext ctx, JsonObject body)
    {
        var parsed = EconomyScenarioLoader.LoadWorkplace(body);
        if (!parsed.IsSuccess) return Result<AuthoredWorkplaceCreated>.Fail(parsed.Error!);
        var spec = parsed.Value!;
        if (!world.PopulationCatalog.IsValidLocationType(spec.LocationType))
            return Result<AuthoredWorkplaceCreated>.Fail("Workplaces[].LocationTypeId: fora do catálogo do período");
        if (spec.Location.X < 0 || spec.Location.Y < 0 || spec.Location.X >= world.Map.Width || spec.Location.Y >= world.Map.Height)
            return Result<AuthoredWorkplaceCreated>.Fail("Workplaces[].X/Y: fora do mapa");
        if (spec.MaxVacancies < 1) return Result<AuthoredWorkplaceCreated>.Fail("Workplaces[].MaxVacancies: precisa ser ao menos 1");
        if (spec.Treasury.Amount < 0) return Result<AuthoredWorkplaceCreated>.Fail("Workplaces[].Treasury: não pode ser negativo");

        var placed = WorkplacePlacer.Place(world, spec);
        if (!placed.IsSuccess) return Result<AuthoredWorkplaceCreated>.Fail(placed.Error!);
        var workplace = placed.Value!;
        if (workplace.Treasury.Amount > 0) world.Mint(ctx, workplace.Treasury, "authored-workplace");
        ctx.LogEvent(
            WorldEventKind.AuthoredWorkplaceFounded,
            $"{workplace.Id.Value}|{workplace.City.Value}|{workplace.LocationType.Id}|{workplace.Location.X}|{workplace.Location.Y}",
            sourceSystem: "WorldAuthoringCommands");
        return Result<AuthoredWorkplaceCreated>.Ok(new AuthoredWorkplaceCreated(
            workplace.Id.Value, workplace.Building?.Value, workplace.City.Value.ToString(), workplace.Location.X, workplace.Location.Y));
    }

    public static Result<Unit> ForceAction(
        WorldState world, TickContext ctx, NpcId npcId, ActionType action)
    {
        if (!Enum.IsDefined(action)) return Result<Unit>.Fail("Action: ação fora do catálogo");
        var npc = world.FindNpc(npcId);
        if (npc is null || !npc.IsAlive) return Result<Unit>.Fail("NpcId: NPC ausente ou morto");
        npc.SetCurrentAction(action, ctx.CurrentTick);
        ctx.LogEvent(WorldEventKind.AuthoringCommandApplied, $"action|{npcId.Value}|{action}", sourceSystem: "WorldAuthoringCommands");
        return Result<Unit>.Ok(Unit.Value);
    }
}

public sealed class WorldAuthoringService(IWorldEventSink sink)
{
    private static TickContext Context(WorldState world, IWorldEventSink sink) =>
        new(world, world.Rng, world.Scheduler, sink);

    private Result<T> Run<T>(WorldState world, string operation, Func<TickContext, Result<T>> command)
    {
        var ctx = Context(world, sink);
        var result = command(ctx);
        if (!result.IsSuccess)
            ctx.LogEvent(WorldEventKind.AuthoringCommandRejected, $"{operation}|{result.Error}", sourceSystem: "WorldAuthoringCommands");
        return result;
    }

    public Result<ExtraordinaryCarrierState> Grant(WorldState world, NpcId npcId, string powerId) =>
        Run(world, "power.grant", ctx => ExtraordinaryStateSystem.GrantAuthored(world, ctx, npcId, powerId));

    public Result<ExtraordinaryCarrierState?> Revoke(WorldState world, NpcId npcId, string powerId) =>
        Run(world, "power.revoke", ctx => ExtraordinaryStateSystem.RevokeAuthored(world, ctx, npcId, powerId));

    public Result<ExtraordinaryInvocationResult> Invoke(
        WorldState world, NpcId carrierId, string powerId, NpcId targetId,
        CellCoord? targetCell, ResolutionResult? resolution) =>
        Run(world, "power.invoke", ctx => ExtraordinaryInvocationEngine.InvokeAuthored(
            world, ctx, carrierId, powerId, targetId, targetCell, resolution));

    public Result<Unit> RewritePersonality(WorldState world, NpcId npcId, PersonalityValues values) =>
        Run(world, "personality", ctx => WorldAuthoringCommands.RewritePersonality(world, ctx, npcId, values));

    public Result<int> BreakRelationships(WorldState world, NpcId first, NpcId second) =>
        Run(world, "relationships.break", ctx => WorldAuthoringCommands.BreakRelationships(world, ctx, first, second));

    /// <summary>Criação ao vivo recusada não escreve nada no mundo (nem o evento de rejeição): o hash fica igual e o motivo vai só
    /// para a auditoria de admin da rota (ADR-0033).</summary>
    public Result<AuthoredHouseholdCreated> CreateHousehold(WorldState world, JsonObject body) =>
        WorldAuthoringCommands.CreateHousehold(world, Context(world, sink), body);

    public Result<AuthoredWorkplaceCreated> CreateWorkplace(WorldState world, JsonObject body) =>
        WorldAuthoringCommands.CreateWorkplace(world, Context(world, sink), body);

    public Result<Unit> ForceAction(WorldState world, NpcId npcId, ActionType action) =>
        Run(world, "action", ctx => WorldAuthoringCommands.ForceAction(world, ctx, npcId, action));
}
