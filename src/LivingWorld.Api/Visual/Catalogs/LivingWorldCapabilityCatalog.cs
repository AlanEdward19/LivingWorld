using System.Reflection;
using LivingWorld.Domain.History;
using LivingWorld.Simulation.Scheduling;

namespace LivingWorld.Api.Visual.Catalogs;

public enum CapabilityKind
{
    LivingWorld,
    DiagnosticOnly,
}

public sealed record LivingWorldCapability(
    string Id,
    CapabilityKind Kind,
    IReadOnlyList<Type> Systems,
    IReadOnlyList<WorldEventKind> Events,
    IReadOnlyList<string> ConsumerKeys,
    string? DiagnosticReason = null);

/// <summary>Capacidades vivas do mundo. Quase tudo é derivado dos próprios sistemas
/// (<see cref="LivingCapabilityAttribute"/>): sistema novo declara a capacidade na própria classe e entra
/// aqui sozinho. Só as capacidades sem sistema próprio (leituras de estado e eventos avulsos) são listadas
/// à mão em <see cref="Standalone"/>.</summary>
public static class LivingWorldCapabilityCatalog
{
    private static readonly LivingWorldCapability[] Standalone =
    [
        Living("TIME", consumers: ["hud.clock"]),
        Living("REST", consumers: ["map.npc.rest"]),
        Living("FOOD", consumers: ["inspector.npc.food"]),
        Living("WATER", consumers: ["map.water"]),
        Living("MONEY", events: [WorldEventKind.Minted, WorldEventKind.Destroyed], consumers: ["timeline.money"]),
        Living("PLAYER", events: [WorldEventKind.PlayerIncarnated, WorldEventKind.PlayerDisappeared], consumers: ["map.npc.action"]),
        Living("AUTHORING", events: [WorldEventKind.AuthoringCommandApplied, WorldEventKind.AuthoringCommandRejected], consumers: ["inspector.npc.authoring"]),
    ];

    // Declarada depois de Standalone: os inicializadores estáticos rodam na ordem do texto.
    public static IReadOnlyList<LivingWorldCapability> All { get; } = [.. Declared(), .. Standalone];

    private static IEnumerable<LivingWorldCapability> Declared()
    {
        var declarations = typeof(ISimulationSystem).Assembly.GetTypes()
            .Select(type => (Type: type, Attribute: type.GetCustomAttribute<LivingCapabilityAttribute>(inherit: false)))
            .Where(item => item.Attribute is not null)
            .OrderBy(item => item.Attribute!.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var group in declarations.GroupBy(item => item.Attribute!.Id))
        {
            var diagnosticReason = group.Select(item => item.Attribute!.DiagnosticReason).FirstOrDefault(reason => reason is not null);
            yield return new LivingWorldCapability(
                group.Key,
                diagnosticReason is null ? CapabilityKind.LivingWorld : CapabilityKind.DiagnosticOnly,
                [.. group.Select(item => item.Type)],
                [.. group.SelectMany(item => item.Attribute!.Events).Distinct()],
                [.. group.SelectMany(item => item.Attribute!.Consumers).Distinct()],
                diagnosticReason);
        }
    }

    private static LivingWorldCapability Living(
        string id,
        IReadOnlyList<WorldEventKind>? events = null,
        IReadOnlyList<string>? consumers = null) =>
        new(id, CapabilityKind.LivingWorld, [], events ?? [], consumers ?? []);
}
