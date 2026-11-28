using System.Text.Json.Serialization;
using LivingWorld.Domain.Geography;

namespace LivingWorld.Domain.Cities.Buildings;

/// <summary>#264: peça comprada (ou entregue por encomenda) numa casa: o objeto do catálogo, o lugar (andar, célula local, giro) e o que se pagou. <c>Gone</c>: vendida ou
/// descartada (o índice na mobília fica, para as luzes e os usos não trocarem de dono).</summary>
public sealed record EditedPiece(string Id, int Variation, int Floor, int X, int Y, int Facing, long Paid = 0, bool Gone = false);

/// <summary>#264: uma peça (índice na mobília, a do catálogo do tipo de prédio ou uma comprada) que mudou de lugar.</summary>
public sealed record PieceMove(int Piece, int Floor, int X, int Y, int Facing);

/// <summary>#264: o estado de uma aresta interior depois da obra: <c>wall</c> (parede levantada), <c>open</c> (parede derrubada) ou <c>door</c> (porta); a última vence.</summary>
public sealed record EdgeEdit(int Floor, int AX, int AY, int BX, int BY, string State)
{
    [JsonIgnore] public WallEdge Edge => WallEdge.Between(new CellCoord(AX, AY), new CellCoord(BX, BY));
}

/// <summary>#264: pintura (id de <c>Home.paints</c>) num piso (célula) ou numa parede (aresta).</summary>
public sealed record PaintMark(string Kind, int Floor, int X, int Y, int BX, int BY, string Paint);

/// <summary>#265: um móvel pago e ainda não posicionado, com o lugar físico em que está (<c>Where</c>): <c>shop</c> (reservado no estoque da loja em nome da casa; com <c>ReadyAtMs</c> no
/// futuro a loja ainda o fabrica), <c>hands</c> (nas mãos de <c>Carrier</c>, com <c>Helper</c> se dois levam), <c>car</c> (no porta-malas do veículo da casa), <c>courier</c> (a caminho nas mãos do
/// entregador <c>Courier</c>, de <c>DepartMs</c> a <c>DueMs</c>) ou <c>depot</c> (o depósito da casa, pronto para posicionar). <c>Seq</c> numera por casa (só cresce).</summary>
public sealed record Parcel(int Seq, string Id, int Variation, long Paid, string Where, long ReadyAtMs = 0, long Carrier = 0, long Helper = 0, long Courier = 0, long DepartMs = 0, long DueMs = 0, long Fee = 0);

/// <summary>#265: o veículo da casa fora do lugar de partida (dirigido): a célula do mundo onde está parado. Nulo = no lugar de partida, em frente à porta.</summary>
public sealed record CarSpot(int X, int Y);

/// <summary>#264: o que o morador mudou na casa (nulo no prédio = nada mudou, fora do snapshot e do hash). Móveis comprados (índice depois dos do catálogo, só cresce), movidos
/// e vendidos; paredes levantadas ou derrubadas, portas, células de piso novas e pinturas; encomendas a caminho. A planta base e os cômodos do planejador continuam funções puras
/// do prédio; isto é o que se aplica por cima (<see cref="HomeEditor"/>).</summary>
public sealed class HomeEdit
{
    public List<EditedPiece> Added { get; set; } = [];
    public List<PieceMove> Moved { get; set; } = [];
    public List<int> Removed { get; set; } = [];
    public List<EdgeEdit> Edges { get; set; } = [];
    public List<CellCoord> Cells { get; set; } = [];
    public List<PaintMark> Paints { get; set; } = [];
    public List<Parcel> Parcels { get; set; } = [];

    /// <summary>Próximo número de móvel pago (só cresce).</summary>
    public int NextParcel { get; set; }

    /// <summary>Onde o carro da casa está, se alguém o dirigiu.</summary>
    public CarSpot? Car { get; set; }

    /// <summary>Contador de execução (fora do snapshot): invalida os caches derivados da planta e dos cômodos.</summary>
    [JsonIgnore] public int Version { get; private set; }

    public void Touch() => Version++;

    [JsonIgnore]
    public bool IsEmpty => Added.Count == 0 && Moved.Count == 0 && Removed.Count == 0 && Edges.Count == 0 && Cells.Count == 0 && Paints.Count == 0 && Parcels.Count == 0 && Car is null;
}
