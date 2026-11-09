using LivingWorld.Domain.Geography;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Extraordinary.Mechanics;

public sealed class HouseholdResourceCostMechanic : ExtraordinaryMechanic
{
    public override string Prefix => "household.resource.";
    public override ExtraordinaryMechanicKind Kind => ExtraordinaryMechanicKind.Cost;

    /// <summary>Parser único de <c>household.resource.&lt;id&gt;:n</c> (carga e invocação).</summary>
    public static readonly ExtraordinaryPrimitive<(ResourceType Resource, int Amount)> Cost = new(
        "household.resource.", "household.resource.<id>:<n>", false, "household",
        static token =>
        {
            var parsed = ExtraordinaryMechanicSupport.ParseAmount(token, "Costs", allowSigned: false);
            if (!parsed.IsSuccess) return Result<(ResourceType, int)>.Fail(parsed.Error!);
            return ExtraordinaryMechanicSupport.TryResource(parsed.Value.Key, out var resource)
                ? Result<(ResourceType, int)>.Ok((resource, parsed.Value.Amount))
                : Result<(ResourceType, int)>.Fail($"Costs: alvo não suportado '{parsed.Value.Key}'");
        });

    public override IReadOnlyList<ExtraordinaryPrimitive> Primitives { get; } = [Cost];

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
        var parsed = Cost.Parse(declaration);
        if (!parsed.IsSuccess) return Result<PreparedMutation>.Fail(parsed.Error!);
        var resource = parsed.Value.Resource;
        string key = declaration[..declaration.LastIndexOf(':')];
        var home = ctx.Carrier.Household is { } householdId
            ? ctx.World.FindHousehold(householdId)
            : null;
        if (home is null)
            return Result<PreparedMutation>.Fail($"Costs: alvo não suportado '{key}'");
        return Result<PreparedMutation>.Ok(new PreparedMutation(declaration, _ => home.Withdraw(resource, amount)));
    }
}
