using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Economy.Credit;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — empréstimo, salário atrasado e falência (AD-155). As regras moram em EconomyRules.Credit.
public sealed partial class WorldState
{
    private CreditLedger? _credit;

    /// <summary>Livro do crédito. Nulo enquanto nunca houve empréstimo, atraso ou aperto: omitido do snapshot e do hash (o mundo de
    /// antes fica com o mesmo hash).</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CreditLedger? Credit => _credit is { IsEmpty: false } ? _credit : null;

    /// <summary>O livro para escrever (criado na primeira vez).</summary>
    internal CreditLedger CreditBook => _credit ??= new CreditLedger();

    /// <summary>Crédito ligado: economia, quem empreende e as regras do crédito.</summary>
    internal bool CreditOn => EconomyRules is { Enabled: true, Credit: not null, Venture: not null };

    /// <summary>Restaura o livro guardado no snapshot (ausente = nunca houve crédito).</summary>
    public void RestoreCredit(CreditLedger? ledger)
    {
        _credit = ledger;
        CanonicalHashCache.MarkPropertyDirty(nameof(Credit));
    }
}
