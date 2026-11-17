namespace LivingWorld.Domain.Cities;

/// <summary>Traçado da cidade (AD-173): a vila cresce ao longo de ruas que saem do ponto de fundação, e cada prédio nasce
/// num lote voltado para a rua. O tamanho do lote (quintal da frente, recuo, quintal de trás) é escolha de quem constrói,
/// pela terra (centro caro × borda), pelos meios (relativos à cidade), pelo ofício e pelo tamanho da família; quando falta
/// terra, desce pelos degraus de <c>PlacementPolicy</c>. Números escolhidos para a escala do modelo (a célula não tem
/// metragem fixa): ver <c>docs/domain/city-layout.md</c>.</summary>
/// <param name="Enabled">Desligado: a colocação de antes (folga de uma célula, varredura dos bounds e anéis), sem ruas.</param>
/// <param name="SquareRadiusCells">Raio (Chebyshev) do largo no ponto de fundação; não recebe prédio.</param>
/// <param name="MaxStreetCells">Comprimento máximo de cada rua (principal para cada lado e travessas), em células.</param>
/// <param name="LaneSpacingCells">Distância, ao longo da rua principal, entre duas travessas: duas fileiras de lotes de costas.</param>
/// <param name="CentralDepthCells">Até esta distância do largo (passos de rua) a terra é cara: só quem tem meios faz quintal.</param>
/// <param name="FrontYardCells">Quintal da frente da casa de lavrador (porta → rua). Oficina e casa de artesão abrem direto na rua.</param>
/// <param name="SideSetbackCells">Recuo entre o prédio e o vizinho, quando quem constrói não escolhe parede-meia.</param>
/// <param name="FarmerCroftCells">Fundo do quintal (croft) da casa de lavrador: horta, bichos, monte de lenha.</param>
/// <param name="ArtisanCroftCells">Fundo do quintal da casa de artesão fora do centro.</param>
/// <param name="LargeFamilyMembers">A partir de quantos moradores a casa de lavrador pede uma faixa a mais de quintal.</param>
/// <param name="PoorMeansFraction">Pobre: dinheiro por pessoa abaixo desta fração da mediana da cidade (casebre, sem quintal, parede-meia).</param>
/// <param name="RichMeansFactor">Remediado: acima deste múltiplo da mediana; paga quintal e recuo mesmo no centro.</param>
/// <param name="CroftValuePerCell">Quanto cada célula de quintal soma ao fator de local do valor do imóvel (aluguel, diária).</param>
/// <param name="TrodResidents">Moradores servidos a partir dos quais a trilha vira caminho de terra batida.</param>
/// <param name="CroftDetourCells">Quantos passos de rua a mais a família anda para ter quintal; além disso, fica com o lote sem
/// quintal mais perto do largo (perto do trabalho e do mercado vale mais que a horta).</param>
/// <param name="ChainedBoundsReachCells">O limite crescido absorve em cadeia (a até <c>AbsorptionRingCells</c> do limite já
/// crescido) o prédio a até esta distância da caixa por população: a vila ao longo da rua continua uma cidade só. Mais longe,
/// a casa que nasceu sem lote perto forma aglomerado à parte, que pode fundar cidade-filha e depois se fundir à mãe. 0 = sem
/// cadeia (só o anel em volta da caixa por população, como antes).</param>
/// <param name="PathCells">Caminho a pé mais longo da soleira até a rua para uma casa fora da frente de rua (beco, trilha).</param>
public sealed record CityLayoutRules(
    bool Enabled = true,
    int SquareRadiusCells = 1,
    int MaxStreetCells = 40,
    int LaneSpacingCells = 13,
    int CentralDepthCells = 5,
    int FrontYardCells = 1,
    int SideSetbackCells = 1,
    int FarmerCroftCells = 2,
    int ArtisanCroftCells = 1,
    int LargeFamilyMembers = 5,
    double PoorMeansFraction = 0.5,
    double RichMeansFactor = 2.0,
    double CroftValuePerCell = 0.02,
    int TrodResidents = 12,
    int CroftDetourCells = 4,
    int PathCells = 8,
    int ChainedBoundsReachCells = 6)
{
    public static CityLayoutRules Default { get; } = new();

    /// <summary>A regra de antes do AD-173, para medir pareado.</summary>
    public static CityLayoutRules Off { get; } = new(Enabled: false);

    /// <summary>Maior quintal da frente possível (alcance da porta até a rua).</summary>
    public int MaxFrontYardCells() => Math.Max(0, FrontYardCells);
}
