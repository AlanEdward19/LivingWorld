using System.Text.Json.Serialization;
using LivingWorld.Domain.Cognition;
using LivingWorld.Domain.Shared;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — cérebro com ganhos sinápticos aprendidos (ADR-0046, AD-142), experimento atrás de flag.
public sealed partial class WorldState
{
    /// <summary>Regras do cérebro. Nulo = experimento desligado (o padrão): omitido do snapshot e do hash canônico, que ficam
    /// idênticos aos de antes; snapshot antigo abre desligado.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BrainRules? BrainRules { get; private set; }

    /// <summary>Cérebro ligado neste mundo.</summary>
    internal bool BrainOn => BrainRules is { Enabled: true };

    /// <summary>Liga o cérebro (criação do mundo, cenário, teste) ou restaura do snapshot. Regras desligadas = nulo.</summary>
    internal void UseBrain(BrainRules? rules)
    {
        BrainRules = rules is { Enabled: true } ? rules : null;
        CanonicalHashCache.MarkPropertyDirty(nameof(BrainRules));
    }
}
