using System.Text.Json.Serialization;
using LivingWorld.Domain.Invention;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Invention;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — invenção por composição física (ADR-0045, AD-141).
public sealed partial class WorldState
{
    /// <summary>Natureza, leis e jeito de inventar do cenário. Nulo = mundo sem invenção: omitido do snapshot e do hash canônico,
    /// que ficam idênticos aos de antes (snapshot antigo abre com nulo).</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InventionRules? InventionRules { get; private set; }

    /// <summary>Designs, quem sabe, instâncias e mentes ocupadas com o problema. Nulo quando a invenção está desligada.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InventionLedger? Invention { get; private set; }

    /// <summary>Contagem do relatório de invenção; nenhum sistema decide por ela.</summary>
    [Volatile, Ephemeral] public InventionTally InventionTally { get; } = new();

    /// <summary>Invenção ligada neste mundo.</summary>
    internal bool InventionOn => InventionRules is { Enabled: true } && Invention is not null;

    /// <summary>Liga a invenção (criação do mundo) ou restaura o estado guardado (snapshot). Regras desligadas = nulo.</summary>
    internal void UseInvention(InventionRules? rules, InventionLedger? ledger = null)
    {
        InventionRules = rules is { Enabled: true } ? rules : null;
        Invention = InventionRules is null ? null : ledger ?? new InventionLedger();
        CanonicalHashCache.MarkPropertyDirty(nameof(InventionRules));
        CanonicalHashCache.MarkPropertyDirty(nameof(Invention));
    }
}
