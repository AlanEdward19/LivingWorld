namespace LivingWorld.Domain.Population.Family;

/// <summary>Estado conjugal de uma pessoa (AD-136), do lado dela. <see cref="SinceTick"/> = início da união atual;
/// <see cref="Grievance"/> = mágoa acumulada com o cônjuge (0..100: brigas, penúria, luto de filho, traição), que a convivência
/// perdoa aos poucos e que baixa o teto do afeto do casal. <see cref="AffairWith"/> = amante atual (caso em curso). Depois de
/// uma separação ficam o ex-cônjuge, o modo e o tick a partir do qual a pessoa pode cortejar de novo.</summary>
public sealed record UnionState(
    long SinceTick = 0,
    double Grievance = 0,
    long? AffairWith = null,
    long AffairSinceTick = 0,
    long? FormerSpouse = null,
    UnionEndMode? EndMode = null,
    long CourtFromTick = 0,
    int Unions = 0);

/// <summary>Como uma união acaba em vida. A frequência de cada modo é norma do cenário (<see cref="UnionRules"/>).</summary>
public enum UnionEndMode
{
    /// <summary>Separação de fato: cada um vai morar num lugar, sem desfazer o vínculo perante a lei ou a igreja.</summary>
    Separation,

    /// <summary>Quem sai foge ou some: o outro fica com a casa e os filhos.</summary>
    Abandonment,

    /// <summary>Anulação: a união é declarada nula e os dois podem casar de novo.</summary>
    Annulment,

    /// <summary>Divórcio formal: os dois podem casar de novo.</summary>
    Divorce,
}
