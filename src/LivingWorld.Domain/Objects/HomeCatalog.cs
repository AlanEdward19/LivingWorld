namespace LivingWorld.Domain.Objects;

/// <summary>#264: comprar, posicionar e construir como dado do cenário (<c>scenarios/catalog/home-build.json</c>, bloco <c>Home</c>): quais objetos do catálogo se vendem, a que preço,
/// com que beleza, em que recurso a loja os guarda e o que custa a obra. O código só conhece o mecanismo (comprar do estoque da loja ou encomendar, pagar de carteira para caixa,
/// validar o lugar, somar a beleza do cômodo); nome, preço e número são dado. Nulo = cenário sem modo comprar (omitido do snapshot e do hash).</summary>
public sealed record HomeCatalog(
    HomeRules Rules,
    IReadOnlyList<HomeCategory> Categories,
    IReadOnlyList<HomeItem> Items,
    IReadOnlyList<HomePaint> Paints,
    HomeBuild Build,
    HomeHaul? Haul = null)
{
    public HomeItem? Item(string objectId) => IndexOf(this).ByObject.GetValueOrDefault(objectId);
    public HomeItem? ItemOfResource(int resourceId) => IndexOf(this).ByResource.GetValueOrDefault(resourceId);
    public HomePaint? Paint(string id) => IndexOf(this).Paints.GetValueOrDefault(id);

    /// <summary>A pintura de partida do tipo (<c>floor</c> ou <c>wall</c>): a marcada <c>default</c>, ou a primeira do tipo.</summary>
    public HomePaint? DefaultPaint(string kind) =>
        Paints.FirstOrDefault(p => p.Kind == kind && p.Default) ?? Paints.FirstOrDefault(p => p.Kind == kind);

    private sealed record Index(Dictionary<string, HomeItem> ByObject, Dictionary<int, HomeItem> ByResource, Dictionary<string, HomePaint> Paints);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HomeCatalog, Index> Indexes = new();

    private static Index IndexOf(HomeCatalog c) => Indexes.GetValue(c, static h =>
    {
        var byObject = new Dictionary<string, HomeItem>();
        var byResource = new Dictionary<int, HomeItem>();
        foreach (var item in h.Items) { byObject.TryAdd(item.Object, item); byResource.TryAdd(item.Resource, item); }
        var paints = new Dictionary<string, HomePaint>();
        foreach (var paint in h.Paints) paints.TryAdd(paint.Id, paint);
        return new Index(byObject, byResource, paints);
    });
}

/// <summary><c>ResalePercent</c>: quanto do preço a loja devolve ao comprar o móvel usado; <c>DeliveryMin</c>: minutos de mundo da compra do estoque à entrega (0 = na hora);
/// <c>OrderMarkupPercent</c>: acréscimo da encomenda (a loja fabrica); <c>MaxPieces</c>: teto de móveis comprados por casa. #268 <c>ExpectedBeauty</c>: a beleza que se espera de cada tipo de cômodo (um banheiro não é feio só por ter vaso e chuveiro): o gatilho de ambiente mede a beleza menos a esperada.</summary>
public sealed record HomeRules(int ResalePercent = 60, int DeliveryMin = 0, int OrderMarkupPercent = 0, int MaxPieces = 60,
    IReadOnlyDictionary<string, double>? ExpectedBeauty = null);

public sealed record HomeCategory(string Id, string Name);

/// <summary>Fabricação sob encomenda: <c>Hours</c> de mundo e os <c>Materials</c> (id do recurso → quantidade) que saem do estoque da loja.</summary>
public sealed record HomeCraft(int Hours, IReadOnlyDictionary<string, long> Materials);

/// <summary>Um móvel à venda: o objeto do catálogo, a categoria, o preço (moeda do mundo), a beleza que dá ao cômodo (pode ser negativa), o recurso em que a loja o guarda, se
/// precisa ficar encostado na parede e em que cômodos pode ficar (nulo = em qualquer um).</summary>
public sealed record HomeItem(
    string Object, string Category, long Price, double Beauty, int Resource, bool Wall = false, IReadOnlyList<string>? Rooms = null, HomeCraft? Craft = null);

/// <summary>Pintura de piso ou de parede: cor (hex), beleza (média no cômodo, vale pela fração pintada) e preço por célula de piso ou por trecho de parede.</summary>
public sealed record HomePaint(string Id, string Name, string Kind, string Color, double Beauty = 0, long Price = 0, bool Default = false);

/// <summary>A obra: <c>Contractor</c> é o tipo de local (<c>LocationType</c>) cujo caixa recebe o que se paga; <c>Wall</c>, <c>Door</c>, <c>Demolish</c> e <c>Cell</c> são o preço
/// de levantar um trecho de parede, abrir uma porta, derrubar e acrescentar uma célula de piso; <c>MaxExtraCells</c> limita a ampliação.</summary>
public sealed record HomeBuild(int Contractor, long Wall = 0, long Door = 0, long Demolish = 0, long Cell = 0, int MaxExtraCells = 12);

/// <summary>#265: como o móvel comprado chega em casa, tudo em dado (<c>Home.haul</c>). <c>Heft</c> diz o peso e o volume de cada móvel (por objeto); <c>Hands</c> o que o corpo leva andando e
/// quanto isso o atrasa; <c>Vehicles</c> e <c>Fleet</c> os veículos (capacidade do porta-malas, velocidade) e quais a casa própria tem estacionados; <c>Delivery</c> o preço e o prazo de quem entrega;
/// <c>ReachM</c> a distância (m) da porta da loja ou da casa em que se pega e se deixa a carga. Sem o bloco, comprar põe o móvel na casa de uma vez (o mundo de antes).</summary>
public sealed record HomeHaul(
    HomeHands Hands, HomeDelivery Delivery, IReadOnlyList<HomeVehicle> Vehicles, IReadOnlyList<string> Fleet, IReadOnlyDictionary<string, HomeHeft> Heft, double ReachM = 4.5)
{
    public HomeHeft? HeftOf(string objectId) => Heft.GetValueOrDefault(objectId);
    public HomeVehicle? Vehicle(string id) => Vehicles.FirstOrDefault(v => v.Id == id);
}

/// <summary>Peso (kg) e volume (litros) de um móvel.</summary>
public sealed record HomeHeft(double Kg, double Liters);

/// <summary>O que cabe nas mãos de um corpo (<c>MaxKg</c>, <c>MaxLiters</c>; com ajuda de outra pessoa, <c>HelperFactor</c> vezes) e quanto atrasa o passo com a carga cheia (<c>SlowPercent</c>, proporcional ao peso).</summary>
public sealed record HomeHands(double MaxKg, double MaxLiters, int SlowPercent, double HelperFactor = 2.0);

/// <summary>Veículo em dado: o porta-malas (<c>TrunkKg</c>, <c>TrunkLiters</c>), a velocidade (m/s) e a cor com que o cliente o desenha.</summary>
public sealed record HomeVehicle(string Id, string Name, double TrunkKg, double TrunkLiters, double Mps, string Color = "9aa5b1");

/// <summary>A entrega pela loja: <c>Fee</c> fixa mais <c>PerKg</c> por quilo; sai entre <c>OpenHour</c> e <c>CloseHour</c> (hora do dia do mundo), <c>PrepMin</c> depois do pedido; o entregador anda a <c>Mps</c> (m/s) carregando.</summary>
public sealed record HomeDelivery(long Fee, long PerKg, int OpenHour, int CloseHour, int PrepMin, double Mps);
