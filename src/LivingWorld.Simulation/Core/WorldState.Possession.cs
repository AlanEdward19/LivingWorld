using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — estranhamento e descoberta da posse (AD-197f).
public sealed partial class WorldState
{
    /// <summary>Quem estranhou qual corpo. Nulo enquanto ninguém estranhou ninguém: omitido do snapshot e do hash.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PossessionLedger? Possession { get; private set; }

    /// <summary>O livro, criado na primeira vez que alguém estranha; marca o hash como sujo (quem chama vai mudar o livro).</summary>
    internal PossessionLedger PossessionBook()
    {
        Possession ??= new PossessionLedger();
        CanonicalHashCache.MarkPropertyDirty(nameof(Possession));
        return Possession;
    }

    /// <summary>Restaura do snapshot.</summary>
    internal void RestorePossession(PossessionLedger? ledger)
    {
        Possession = ledger is { Suspicions.Count: > 0 } ? ledger : null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Possession));
    }
}
