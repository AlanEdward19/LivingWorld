using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Extraordinary.Mechanics;

namespace LivingWorld.Simulation.Extraordinary.Engine;

/// <summary>Uma primitiva que o motor aceita, como um editor a veria (uso interno; a rota
/// pública é decisão separada).</summary>
public sealed record ExtraordinaryPrimitiveInfo(
    string Mechanic,
    string Head,
    string Format,
    ExtraordinaryMechanicKind Kind,
    bool SignedMagnitude,
    string Target);

/// <summary>
/// Catálogo de primitivas derivado do registro por reflexão (<see cref="Type"/> de cada mecânica
/// registrada + <see cref="IExtraordinaryMechanic.Primitives"/>): nenhuma lista paralela.
/// Também valida tokens de cenário contra o que o motor aceita.
/// </summary>
public static class ExtraordinaryPrimitiveCatalog
{
    public static IReadOnlyList<ExtraordinaryPrimitiveInfo> Describe(ExtraordinaryMechanicRegistry registry) =>
        registry.All
            .SelectMany(mechanic => mechanic.Primitives.SelectMany(primitive => primitive.Listing.Select(
                line => new ExtraordinaryPrimitiveInfo(
                    mechanic.GetType().Name, line.Head, line.Format, mechanic.Kind,
                    line.SignedMagnitude, line.Target))))
            .OrderBy(info => info.Head, StringComparer.Ordinal)
            .ToList();

    /// <summary>Recusa token que nenhuma mecânica do tipo (efeito ou custo) aceite.</summary>
    public static Result<string> Validate(
        IExtraordinaryMechanicRegistry registry, string token, ExtraordinaryMechanicKind kind)
    {
        var mechanic = registry.Resolve(token);
        if (mechanic is null || mechanic.Kind != kind || !mechanic.Primitives.Any(item => item.Matches(token)))
            return Result<string>.Fail($"token '{token}' não é aceito por nenhuma mecânica de {Label(kind)}");
        return Result<string>.Ok(token);
    }

    private static string Label(ExtraordinaryMechanicKind kind) =>
        kind == ExtraordinaryMechanicKind.Effect ? "efeito" : "custo";
}
