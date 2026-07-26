namespace LivingWorld.Domain;

/// <summary>Base para IDs tipados: um wrapper sobre <see cref="Guid"/> que impede
/// trocar um NpcId por um CityId por engano na assinatura de um método.</summary>
public readonly record struct NpcId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct CityId(Guid Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct LocationId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
