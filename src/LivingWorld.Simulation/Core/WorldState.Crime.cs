using System.Text.Json.Serialization;
using LivingWorld.Domain.Crime;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Crime;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — violência, justiça e herança (AD-150). O furto continua em TheftRules (construtor).
public sealed partial class WorldState
{
    /// <summary>Violência, justiça e herança do cenário. Nulo = mundo antigo (snapshot anterior ao AD-150): omitido do
    /// snapshot e do hash, sem briga nem corte, herança toda ao cônjuge.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CrimeRules? CrimeRules { get; private set; }

    /// <summary>Mágoas, casos, fichas e medo por cidade. Nulo com o crime desligado.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CrimeLedger? Crime { get; private set; }

    internal bool CrimeOn => CrimeRules is { Enabled: true } && Crime is not null;

    /// <summary>Liga violência, justiça e herança (criação do mundo) ou restaura o estado guardado (snapshot).</summary>
    public void UseCrime(CrimeRules? rules, CrimeLedger? ledger = null)
    {
        CrimeRules = rules is { Enabled: true } ? rules : null;
        Crime = CrimeRules is null ? null : ledger ?? new CrimeLedger();
        CanonicalHashCache.MarkPropertyDirty(nameof(CrimeRules));
        CanonicalHashCache.MarkPropertyDirty(nameof(Crime));
    }
}
