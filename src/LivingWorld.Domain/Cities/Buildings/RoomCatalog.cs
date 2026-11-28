namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>#226: um tipo de cômodo do cenário (dado, não código). <c>Id</c> é o nome que o cenário dá (sala, quarto, cozinha, oficina...);
/// <c>Uses</c> são os ids de <c>ActionType</c> que o cômodo atende (a mobília cujo ponto de uso atende a uma delas vai para ele);
/// <c>Weight</c> é a proporção da área entre os cômodos da planta; <c>MinCells</c> é o menor tamanho (em células) com que o cômodo existe.</summary>
public sealed record RoomKind(string Id, IReadOnlyList<int> Uses, double Weight = 1, int MinCells = 1);

/// <summary>Catálogo de cômodos do cenário: os tipos e, por tipo de prédio (chave "default" ou o id numérico), a lista ordenada de cômodos do térreo
/// (o primeiro é o que a porta da rua abre; os seguintes se encadeiam a partir dele). A chave <c>"&lt;tipo&gt;@up"</c> (ou <c>"default@up"</c>)
/// lista os cômodos dos andares de cima; sem ela o andar é um cômodo só. Nulo no catálogo de objetos = cenário sem cômodos (um cômodo só).</summary>
public sealed record RoomCatalog(IReadOnlyList<RoomKind> Kinds, IReadOnlyDictionary<string, IReadOnlyList<string>> Plans,
    IReadOnlyDictionary<string, BuildingShape>? Shapes = null)
{
    public const string UpperSuffix = "@up";

    /// <summary>O tamanho da planta que o cenário declara para o tipo de prédio (<c>Rooms.shapes</c>, chave = id do tipo); nulo = o de sempre.</summary>
    public BuildingShape? ShapeFor(int buildingTypeId) =>
        Shapes is not null && Shapes.TryGetValue(buildingTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture), out var shape) ? shape : null;

    /// <summary>Os cômodos do prédio nesse andar, na ordem do plano; vazio = sem divisão (um cômodo só).</summary>
    public IReadOnlyList<RoomKind> PlanFor(int buildingTypeId, int floor)
    {
        string suffix = floor > 0 ? UpperSuffix : "";
        string key = buildingTypeId.ToString(System.Globalization.CultureInfo.InvariantCulture) + suffix;
        if (!Plans.TryGetValue(key, out var ids) && !Plans.TryGetValue("default" + suffix, out ids)) return [];
        var list = new List<RoomKind>(ids.Count);
        foreach (var id in ids)
            foreach (var kind in Kinds)
                if (kind.Id == id) { list.Add(kind); break; }
        return list;
    }
}
