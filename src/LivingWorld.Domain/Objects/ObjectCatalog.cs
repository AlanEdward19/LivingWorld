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

/// <summary>Descritor imutável de um objeto (#47 fatia 1, ADR-0034). <c>Category</c> é string do cenário, não enum.
/// <c>VariationSeed</c> 0 = sem variação (forma exatamente a declarada).</summary>
public sealed record ObjectDescriptor(
    string Id, string Category, IReadOnlyList<ObjectPart> Shape, int VariationSeed, IReadOnlyList<ObjectUsePoint> UsePoints);

/// <summary>Catálogo de objetos do cenário: descritores e a mobília por tipo de prédio (chave "default" ou o id numérico).
/// Nulo no mundo = cenário sem <c>Objects</c>: omitido do snapshot e do hash (opt-in).</summary>
public sealed record ObjectCatalog(
    int Format, IReadOnlyList<ObjectDescriptor> Objects, IReadOnlyDictionary<string, IReadOnlyList<string>> Furnishings)
{
    public const int CurrentFormat = 1;

    public ObjectDescriptor? Find(string id) => Objects.FirstOrDefault(o => o.Id == id);

    /// <summary>Ids de mobília do tipo de prédio; cai em "default"; vazio se nenhum dos dois existe.</summary>
    public IReadOnlyList<string> FurnishingFor(int buildingTypeId) =>
        Furnishings.TryGetValue(buildingTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var own) ? own
        : Furnishings.TryGetValue("default", out var fallback) ? fallback
        : Array.Empty<string>();
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
