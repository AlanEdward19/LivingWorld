using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class HouseholdResourceCostMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "household.resource.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Cost;

    public override Result<long> CostAvailable(ExtraordinaryMechanicContext ctx, string key)
    {
        if (!ExtraordinaryMechanicSupport.TryResource(key, out var resource))
            return Result<long>.Fail($"Costs: alvo não suportado '{key}'");
        var home = ctx.Carrier.Household is { } householdId
            ? ctx.World.FindHousehold(householdId)
            : null;
        if (home is null)
            return Result<long>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<long>.Ok(home.Stock.GetValueOrDefault(resource));
    }

    public override Result<PreparedMutation> PrepareCost(
        ExtraordinaryMechanicContext ctx, string declaration, int amount)
    {
        var parsed = ExtraordinaryMechanicSupport.ParseAmount(declaration, "Costs", allowSigned: false);
        if (!parsed.IsSuccess) return Result<PreparedMutation>.Fail(parsed.Error!);
        string key = parsed.Value.Key;
        if (!ExtraordinaryMechanicSupport.TryResource(key, out var resource))
            return Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{key}'");
        var home = ctx.Carrier.Household is { } householdId
            ? ctx.World.FindHousehold(householdId)
            : null;
        if (home is null)
            return Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<PreparedMutation>.Ok(new PreparedMutation(declaration, _ => home.Withdraw(resource, amount)));
    }
}
