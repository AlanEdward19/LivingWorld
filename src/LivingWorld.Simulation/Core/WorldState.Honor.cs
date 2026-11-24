using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Population.Naming;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — boa fama dos feitos (AD-183). As regras moram em FamilyRules.Names.
public sealed partial class WorldState
{
    private HonorLedger? _honor;

    /// <summary>Livro da boa fama. Nulo enquanto ninguém fez nada de que se fale: omitido do snapshot e do hash (o mundo de
    /// antes fica com o mesmo hash).</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HonorLedger? Honor => _honor is { Records.Count: > 0 } ? _honor : null;

    /// <summary>O livro para escrever (criado na primeira vez).</summary>
    internal HonorLedger HonorBook => _honor ??= new HonorLedger();

    /// <summary>Memória do que a cidade sabe das linhagens no tick (derivada; fora do snapshot e do hash).</summary>
    internal LineageFame? LineageFameMemo { get; set; }

    public void RestoreHonor(HonorLedger? ledger)
    {
        _honor = ledger;
        LineageFameMemo = null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Honor));
    }
}
