using System.Text.Json.Serialization;
using LivingWorld.Domain.Shared;
using LivingWorld.Simulation.Players;

namespace LivingWorld.Simulation.Core;

// Parte de WorldState — a madeira das árvores tocadas (#243).
public sealed partial class WorldState
{
    /// <summary>As árvores cortadas em parte ou até o toco. Nulo enquanto ninguém cortou nada: omitido do snapshot e do hash (o mundo
    /// sem corte fica idêntico ao de antes); a árvore natural vem da regra de <c>PlayerNature</c>, sem custo por árvore.</summary>
    [Canonical, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TreeLedger? Trees { get; private set; }

    /// <summary>O livro, criado no primeiro corte; marca o hash como sujo (quem chama vai mudar o livro).</summary>
    internal TreeLedger TreeBook()
    {
        Trees ??= new TreeLedger();
        CanonicalHashCache.MarkPropertyDirty(nameof(Trees));
        return Trees;
    }

    /// <summary>Restaura do snapshot; sem nenhuma árvore tocada, volta a nulo.</summary>
    internal void RestoreTrees(TreeLedger? ledger)
    {
        Trees = ledger is { Cuts.Count: > 0 } ? ledger : null;
        CanonicalHashCache.MarkPropertyDirty(nameof(Trees));
    }

    /// <summary>Quando o livro esvazia (tudo cresceu de volta) volta a nulo, para o hash ser o de um mundo sem corte.</summary>
    internal void DropEmptyTrees()
    {
        if (Trees is { Cuts.Count: 0 }) Trees = null;
    }
}
