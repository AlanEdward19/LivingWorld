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

    /// <summary>Alvos de objetos de recurso (#47 fatia 4, ADR-0039): nível (0–100) a que a necessidade é restaurada ao consumir 1 unidade.</summary>
    public const string Hunger = "hunger";
    public const string Thirst = "thirst";

    public static readonly IReadOnlyList<string> Known = [Sleep, Eat];
    public static readonly IReadOnlyList<string> ResourceTargets = [Hunger, Thirst];
}

/// <summary>Descritor imutável de um objeto (#47 fatia 1, ADR-0034). <c>Category</c> é string do cenário, não enum.
/// <c>VariationSeed</c> 0 = sem variação (forma exatamente a declarada). <c>Interactions</c> e <c>Effects</c> (fatia 3) são
/// opcionais e omitidos do snapshot quando nulos, para o hash de catálogos antigos não mudar. <c>Name</c> (#252) é o nome que o cenário dá ao objeto
/// para quem joga ("Fogão"); sem ele, o id.</summary>
public sealed record ObjectDescriptor(
    string Id, string Category, IReadOnlyList<ObjectPart> Shape, int VariationSeed, IReadOnlyList<ObjectUsePoint> UsePoints,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectInteraction>? Interactions = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectEffect>? Effects = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? ResourceId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ObjectUseDef>? Uses = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Rooms = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ObjectLight? Light = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? Footprint = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Near = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ObjectModel? Model = null);

/// <summary>#266: o modelo procedural do móvel como dado: <c>Kind</c> escolhe o gerador genérico do cliente (cama, sofá, geladeira...), <c>SizeM</c> é [largura, profundidade, altura]
/// em metros (cabe na pegada: <see cref="ObjectFootprint"/>, célula de 1,5 m) <c>Style</c> dá as cores por material (madeira, tecido, metal, cerâmica, destaque) e <c>Align</c> diz onde o modelo fica na pegada (<c>back</c>: encostado no fundo, junto à parede; <c>center</c>: no meio). O código do cliente só
/// conhece os geradores; o que cada móvel é (tamanho, cor, tipo) vem do cenário.</summary>
public sealed record ObjectModel(string Kind, IReadOnlyList<double> SizeM, IReadOnlyDictionary<string, string>? Style = null, string Align = "back");

/// <summary>#266: a pegada do móvel em células da planta (largura, profundidade), com a frente voltada para +z e o fundo para -z no giro 0; sem <c>Footprint</c> no descritor vale 1×1.
/// A célula de origem (<c>Spot.Cell</c>) é a de trás à esquerda (a que fica em (0,0) do móvel); o giro (0, 90, 180, 270) gira a pegada em volta dela. Funções puras.</summary>
public static class ObjectFootprint
{
    /// <summary>Largura e profundidade em células (mínimo 1 cada); 1×1 sem declaração.</summary>
    public static (int W, int D) SizeOf(ObjectDescriptor? descriptor) =>
        descriptor?.Footprint is { Count: >= 2 } f ? (Math.Max(1, f[0]), Math.Max(1, f[1])) : (1, 1);

    /// <summary>Deslocamento em células de um ponto local (i à direita, j à frente) para o mundo no giro dado.</summary>
    public static (double X, double Y) Turn(double i, double j, int facing) => facing switch
    {
        90 => (j, -i),
        180 => (-i, -j),
        270 => (-j, i),
        _ => (i, j),
    };

    /// <summary>As células que a peça ocupa com a origem em (<paramref name="ox"/>, <paramref name="oy"/>).</summary>
    public static IEnumerable<(int X, int Y)> Cells(int ox, int oy, int w, int d, int facing)
    {
        for (int j = 0; j < d; j++)
            for (int i = 0; i < w; i++)
            {
                var (dx, dy) = Turn(i, j, facing);
                yield return (ox + (int)dx, oy + (int)dy);
            }
    }

    /// <summary>O deslocamento do centro da pegada em relação ao centro da célula de origem, em células (o cliente desenha o modelo ali).</summary>
    public static (double X, double Y) CenterOffset(int w, int d, int facing) => Turn((w - 1) / 2.0, (d - 1) / 2.0, facing);

    /// <summary>A direção (dx, dy) para onde fica o fundo da peça no giro dado (a parede).</summary>
    public static (int X, int Y) Back(int facing) => facing switch { 90 => (-1, 0), 180 => (0, 1), 270 => (1, 0), _ => (0, -1) };
}

/// <summary>#260: o objeto dá luz (lâmpada, abajur): cor (hex), alcance em metros, intensidade relativa e altura da luz acima do chão em metros. O cliente a
/// acende quando o motor diz que é noite e a casa está ocupada; o objeto não tem estado de ligado e desligado ainda.</summary>
public sealed record ObjectLight(string Color, double RangeM, double Energy, double HeightM);

/// <summary>Catálogo de objetos do cenário: descritores e a mobília por tipo de prédio (chave "default" ou o id numérico).
/// Nulo no mundo = cenário sem <c>Objects</c>: omitido do snapshot e do hash (opt-in).</summary>
public sealed record ObjectCatalog(
    int Format, IReadOnlyList<ObjectDescriptor> Objects, IReadOnlyDictionary<string, IReadOnlyList<string>> Furnishings,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] LivingWorld.Domain.Cities.Buildings.RoomCatalog? Rooms = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NeedCatalog? Needs = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SocialCatalog? Social = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MoodCatalog? Moods = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CareerCatalog? Careers = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeCatalog? Home = null)
{
    public const int CurrentFormat = 1;

    public ObjectDescriptor? Find(string id) => IndexOf(this).ById.GetValueOrDefault(id);

    /// <summary>Descritor do recurso do cenário (objeto com <c>ResourceId</c>), ou nulo se o recurso não tem descritor.</summary>
    public ObjectDescriptor? ForResource(int resourceId) => IndexOf(this).ByResource.GetValueOrDefault(resourceId);

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
    private sealed record Index(
        Dictionary<string, ObjectDescriptor> ById, Dictionary<int, IReadOnlyList<string>> ByType, Dictionary<int, ObjectDescriptor> ByResource);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ObjectCatalog, Index> Indexes = new();

    private static Index IndexOf(ObjectCatalog catalog) => Indexes.GetValue(catalog, static c =>
    {
        var byId = new Dictionary<string, ObjectDescriptor>(c.Objects.Count);
        var byResource = new Dictionary<int, ObjectDescriptor>();
        for (int i = 0; i < c.Objects.Count; i++)
        {
            byId.TryAdd(c.Objects[i].Id, c.Objects[i]);
            if (c.Objects[i].ResourceId is int resourceId) byResource.TryAdd(resourceId, c.Objects[i]);
        }
        var byType = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var (key, ids) in c.Furnishings)
            if (int.TryParse(key, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int typeId))
                byType[typeId] = ids;
        return new Index(byId, byType, byResource);
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
