using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Careers;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — o livro de carreiras e contas da casa (#263).
public sealed partial class WorldState
{
    /// <summary>Empregos, contas e avisos. Nulo enquanto o cenário não emprega ninguém: omitido do snapshot e do hash (o mundo sem carreira fica idêntico ao de antes).</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CareerLedger? Careers { get; private set; }

    /// <summary>O livro, criado na primeira vez que algo entra nele.</summary>
    internal CareerLedger CareersLedger()
    {
        if (Careers is null)
        {
            Careers = new CareerLedger();
            CanonicalHashCache.MarkPropertyDirty(nameof(Careers));
        }

        return Careers;
    }

    /// <summary>Marca o livro como mudado (o hash o relê); chamado depois de qualquer mutação dele.</summary>
    internal void CareersChanged() => CanonicalHashCache.MarkPropertyDirty(nameof(Careers));

    /// <summary>Restaura do snapshot; livro vazio volta a nulo.</summary>
    internal void RestoreCareers(CareerLedger? ledger)
    {
        Careers = ledger is { Jobs.Count: > 0 } or { Bills.Count: > 0 } or { Notices.Count: > 0 } or { Searches.Count: > 0 } or { FreeWillSet.Count: > 0 } ? ledger : null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Careers));
    }
}
