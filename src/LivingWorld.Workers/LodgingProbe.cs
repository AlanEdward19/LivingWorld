using System.Text.Json.Nodes;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Cities;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Scenarios;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Workers;

internal static class LodgingProbe
{
    public static string Run(ulong seed, long ticks)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LivingWorld.sln"))) directory = directory.Parent;
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(directory!.FullName, "scenarios/default.json")))!;
        root["Seed"] = seed;
        root["BuildingRecipes"]!["12"] = new JsonObject
        {
            ["Inputs"] = new JsonObject(),
            ["TicksToBuild"] = 1,
            ["HousingCapacityProvided"] = 3,
            ["Lodging"] = new JsonObject { ["RentPerMonth"] = 100, ["GraceMonths"] = 1, ["NightlyRate"] = 40, ["GuestMinDistance"] = 3 },
            ["Pricing"] = new JsonObject { ["BaseValue"] = 10000, ["RentYieldPerMonth"] = 0.01, ["MinMultiplier"] = 1, ["MaxMultiplier"] = 1 },
        };
        root["Workplaces"]!.AsArray().Add(new JsonObject
        {
            ["LocationTypeId"] = 12,
            ["X"] = 9,
            ["Y"] = 9,
            ["MaxVacancies"] = 2,
            ["Treasury"] = 0,
            ["Stock"] = new JsonObject(),
            ["Prices"] = new JsonObject(),
        });
        var loaded = ScenarioLoaderV2.LoadWorld(root.ToJsonString());
        if (!loaded.IsSuccess) throw new InvalidOperationException(loaded.Error);
        var (world, clock) = loaded.Value;
        var inn = world.Buildings.Single(b => b.BuildingTypeId == 12);
        var tenant = world.Households.OrderBy(h => h.Id.Value).First();
        tenant.LiveIn(inn.Id);
        world.Npcs.Single(n => n.Id == tenant.Head).CreditWallet(new Money(500));
        var landlord = world.Workplaces.Single(w => w.Building == inn.Id);
        long before = landlord.Treasury.Amount;
        new LodgingSystem().Tick(world, new TickContext(world, new WorldRngRegistry(seed), new EventScheduler()));
        long paid = landlord.Treasury.Amount - before;
        clock.Run(world, ticks);
        return $"{WorldSnapshot.CanonicalHash(world)};{paid}";
    }
}
