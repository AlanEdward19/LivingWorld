using LivingWorld.Domain.Behavior;

namespace LivingWorld.Api.Visual.Catalogs;

public sealed record ActionLabel(int ActionType, string Label);

/// <summary>Rótulo de apresentação de cada <see cref="ActionType"/>, servido ao cliente para que ele
/// nunca escreva texto de ação por conta própria. Valor sem rótulo estoura aqui e no teste de cobertura.</summary>
public static class ActionLabelCatalog
{
    public static string LabelOf(ActionType action) => action switch
    {
        ActionType.Eat => "Comendo",
        ActionType.Sleep => "Dormindo",
        ActionType.Work => "Trabalhando",
        ActionType.Socialize => "Conversando",
        ActionType.Travel => "Viajando",
        ActionType.Idle => "Parado",
        ActionType.Buy => "Comprando",
        ActionType.UsePower => "Usando um poder",
        ActionType.Steal => "Furtando",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "ActionType sem rótulo de apresentação"),
    };

    public static IReadOnlyList<ActionLabel> All() =>
        Enum.GetValues<ActionType>().Select(a => new ActionLabel((int)a, LabelOf(a))).ToArray();

    public static void MapActionLabelEndpoints(this WebApplication app) =>
        app.MapGet("/visual/catalog/actions", () => Results.Ok(All()));
}
