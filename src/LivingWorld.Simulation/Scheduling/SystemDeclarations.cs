using LivingWorld.Domain.History;

namespace LivingWorld.Simulation.Scheduling;

/// <summary>O sistema declara a capacidade viva a que pertence. O catálogo de capacidades
/// (<c>LivingWorldCapabilityCatalog</c>) é derivado destas declarações: sistema novo = classe + este atributo,
/// sem editar um catálogo à parte. Vários sistemas podem somar à mesma <see cref="Id"/> (eventos e
/// consumidores são unidos); <see cref="DiagnosticReason"/> marca instrumentação que não descreve o mundo.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class LivingCapabilityAttribute(string id) : Attribute
{
    public string Id { get; } = id;

    /// <summary>Eventos do mundo que este sistema emite sob a capacidade.</summary>
    public WorldEventKind[] Events { get; init; } = [];

    /// <summary>Chaves dos consumidores (HUD, mapa, inspetor) que leem a capacidade.</summary>
    public string[] Consumers { get; init; } = [];

    public string? DiagnosticReason { get; init; }
}

/// <summary>Restrição de ordem no relógio: este sistema roda depois de <see cref="Predecessor"/> no mesmo tick.
/// Substitui a prosa de <c>ScenarioRunner.DefaultSystems</c>; um teste confere a lista real contra
/// as restrições, então reordenar sem quebrar a causalidade não exige editar lista fixa.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RunsAfterAttribute(Type predecessor) : Attribute
{
    public Type Predecessor { get; } = predecessor;
}
