namespace LivingWorld.Domain;

/// <summary>Base para IDs tipados: um wrapper que impede trocar um NpcId por um CityId por
/// engano na assinatura de um método.</summary>
/// <remarks><see cref="NpcId"/> e <see cref="HouseholdId"/> vêm de contador monotônico do
/// <c>WorldState</c> (Fase 3) — <c>Guid.NewGuid()</c> é banido em Domain/Simulation
/// (rules/simulation-determinism.md), então o id precisa nascer determinístico.</remarks>
public readonly record struct NpcId(long Value)
{
    public override string ToString() => $"npc-{Value}";
}

public readonly record struct HouseholdId(long Value)
{
    public override string ToString() => $"household-{Value}";
}

public readonly record struct CityId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct LocationId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
