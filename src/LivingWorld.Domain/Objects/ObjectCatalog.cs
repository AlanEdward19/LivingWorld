namespace LivingWorld.Domain.Objects;

/// <summary>Parte primitiva de um objeto. <c>Shape</c>: box [x,y,z] · cylinder [raio,altura] · sphere [raio] · capsule [raio,altura].
/// <c>Symmetry</c>: none | x | z | xz (a parte é espelhada nos eixos citados). Só dado de forma: quem desenha é o cliente.</summary>
public sealed record ObjectPart(string Shape, IReadOnlyList<double> Size, IReadOnlyList<double> Offset, string Color, string Symmetry = "none");

/// <summary>Onde o corpo encosta no objeto (offset local em metros, a partir da base) e para onde olha (graus em torno de Y).
/// <c>Action</c> (id de <c>ActionType</c>) é a ação que o ponto atende; <c>Pose</c> é a pose do NPC ali (vocabulário em <see cref="ObjectPoses"/>).
/// Sem <c>Action</c> o ponto não atrai ninguém; sem <c>Pose</c> vale a pose da ação (#47 fatia 2, ADR-0037).</summary>
public sealed record ObjectUsePoint(string Name, IReadOnlyList<double> Offset, double Facing, int? Action = null, string? Pose = null);

/// <summary>Poses que o cliente sabe desenhar (espelha <c>animation_catalog.gd</c>; um teste confere a paridade).</summary>
public static class ObjectPoses
{
    public static readonly IReadOnlyList<string> Known = ["idle", "walk", "sleep", "work", "eat", "socialize", "buy", "power"];
}

/// <summary>Ação que o objeto permite e quem pode fazê-la (#47 fatia 3, ADR-0038): <c>Action</c> é o id de <c>ActionType</c>;
/// <c>Profession</c> e <c>SkillId</c>+<c>SkillMin</c> são requisitos opcionais (o NPC precisa cumprir todos os declarados).</summary>
public sealed record ObjectInteraction(int Action, int? Profession = null, int? SkillId = null, double? SkillMin = null);

/// <summary>Efeito do uso no formato <c>alvo:magnitude</c> (Fase 16). Alvos: <see cref="ObjectEffectTargets"/>.
/// <c>Magnitude</c> &gt; -1: a duração da ação é dividida por (1 + magnitude).</summary>
public sealed record ObjectEffect(string Target, double Magnitude);

public static class ObjectEffectTargets
{
    public const string Sleep = "sleep";
    public const string Eat = "eat";
    public static readonly IReadOnlyList<string> Known = [Sleep, Eat];
}

/// <summary>Descritor imutável de um objeto (#47 fatia 1, ADR-0034). <c>Category</c> é string do cenário, não enum.
/// <c>VariationSeed</c> 0 = sem variação (forma exatamente a declarada). <c>Interactions</c> e <c>Effects</c> (fatia 3) são
/// opcionais e omitidos do snapshot quando nulos, para o hash de catálogos antigos não mudar.</summary>
public sealed record ObjectDescriptor(
    string Id, string Category, IReadOnlyList<ObjectPart> Shape, int VariationSeed, IReadOnlyList<ObjectUsePoint> UsePoints,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectInteraction>? Interactions = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectEffect>? Effects = null);

/// <summary>Catálogo de objetos do cenário: descritores e a mobília por tipo de prédio (chave "default" ou o id numérico).
/// Nulo no mundo = cenário sem <c>Objects</c>: omitido do snapshot e do hash (opt-in).</summary>
public sealed record ObjectCatalog(
    int Format, IReadOnlyList<ObjectDescriptor> Objects, IReadOnlyDictionary<string, IReadOnlyList<string>> Furnishings)
{
    public const int CurrentFormat = 1;

    public ObjectDescriptor? Find(string id) => IndexOf(this).ById.GetValueOrDefault(id);

    /// <summary>Ids de mobília do tipo de prédio; cai em "default"; vazio se nenhum dos dois existe.</summary>
    public IReadOnlyList<string> FurnishingFor(int buildingTypeId)
    {
        var index = IndexOf(this);
        return index.ByType.TryGetValue(buildingTypeId, out var own) ? own
            : Furnishings.TryGetValue("default", out var fallback) ? fallback
            : Array.Empty<string>();
    }

    // Índices derivados fora do record (a igualdade de record compararia campos privados): o motor consulta o catálogo a cada
    // decisão de dormir/comer, então busca por id e por tipo de prédio não pode alocar nem varrer texto.
    private sealed record Index(Dictionary<string, ObjectDescriptor> ById, Dictionary<int, IReadOnlyList<string>> ByType);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ObjectCatalog, Index> Indexes = new();

    private static Index IndexOf(ObjectCatalog catalog) => Indexes.GetValue(catalog, static c =>
    {
        var byId = new Dictionary<string, ObjectDescriptor>(c.Objects.Count);
        for (int i = 0; i < c.Objects.Count; i++)
            byId.TryAdd(c.Objects[i].Id, c.Objects[i]);
        var byType = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var (key, ids) in c.Furnishings)
            if (int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int typeId))
                byType[typeId] = ids;
        return new Index(byId, byType);
    });
}

/// <summary>Variação determinística: mesma (seed, posição) → mesmo índice. Seed 0 nunca varia.</summary>
public static class ObjectVariation
{
    public const int Count = 4;

    public static int Index(int variationSeed, int slot)
    {
        if (variationSeed == 0) return 0;
        unchecked
        {
            uint mixed = (uint)variationSeed * 2654435761u ^ (uint)(slot + 1) * 2246822519u;
            mixed ^= mixed >> 16;
            mixed *= 2246822507u;
            mixed ^= mixed >> 13;
            mixed *= 3266489909u;
            mixed ^= mixed >> 16;
            return (int)(mixed % Count);
        }
    }
}
