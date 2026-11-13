namespace LivingWorld.Domain.Invention;

/// <summary>Invenção por composição física (ADR-0045, AD-141), dado de cenário com padrão de época. Três grupos: a natureza
/// (<see cref="Materials"/> e a água), as leis do avaliador (coeficientes de arrasto, potência humana) e o jeito de inventar
/// (frustração, gatilho, busca, ensino, cópia). Nenhum artefato: o cenário nunca declara design, só matéria. Os números do mundo
/// real vêm do meio das faixas que lembramos e precisam de conferência (lista no registro AD-141). <see cref="Disabled"/> = mundo
/// de antes byte a byte (nada de travessia de água, nada de design).</summary>
public sealed record InventionRules
{
    public bool Enabled { get; init; } = true;

    // --- natureza ---

    public IReadOnlyList<MaterialSpec> Materials { get; init; } = DefaultMaterials;

    /// <summary>Água doce, kg/m³.</summary>
    public int WaterDensityKgM3 { get; init; } = 1000;

    /// <summary>Gravidade em mm/s².</summary>
    public int GravityMmS2 { get; init; } = 9807;

    // --- corpo e leis do avaliador ---

    /// <summary>Potência que uma pessoa sustenta remando por horas, W.</summary>
    public int HumanPowerW { get; init; } = 75;

    /// <summary>Velocidade de caminhada, mm/s (≈ 4,7 km/h).</summary>
    public int WalkPaceMmS { get; init; } = 1300;

    /// <summary>Eficiência propulsiva de uma parte chata articulada que empurra a água (‰) e das mãos sem nada (‰).</summary>
    public int PlateEfficiencyPm { get; init; } = 600;
    public int HandEfficiencyPm { get; init; } = 150;

    /// <summary>Coeficiente de arrasto de pressão por primitiva (‰): caixa, cilindro de ponta, esfera, cápsula.</summary>
    public int BoxDragPm { get; init; } = 1050;
    public int CylinderDragPm { get; init; } = 820;
    public int SphereDragPm { get; init; } = 470;
    public int CapsuleDragPm { get; init; } = 300;

    /// <summary>Coeficiente de atrito de superfície molhada (partes por milhão).</summary>
    public int SkinFrictionPpm { get; init; } = 4000;

    /// <summary>Centro de massa de quem vai sentado, acima do assento, mm.</summary>
    public int SeatedCenterOfMassMm { get; init; } = 300;

    /// <summary>Altura metacêntrica mínima para não virar, mm.</summary>
    public int MinMetacentricHeightMm { get; init; } = 30;

    /// <summary>Borda livre mínima de um casco oco carregado, mm.</summary>
    public int MinFreeboardMm { get; init; } = 80;

    /// <summary>Fator de segurança da resistência (‰): tensão admissível = resistência / fator.</summary>
    public int SafetyFactorPm { get; init; } = 2000;

    /// <summary>Velocidade abaixo da qual não se atravessa (a correnteza leva), mm/s.</summary>
    public int MinUsefulPaceMmS { get; init; } = 300;

    /// <summary>Impermeabilidade (‰) a partir da qual uma parte oca segura a água fora.</summary>
    public int WatertightPm { get; init; } = 600;

    /// <summary>Espessura de uma camada de revestimento, mm.</summary>
    public int CoatingMm { get; init; } = 2;

    /// <summary>Eficiência da junção ao transmitir esforço para a estrutura (‰): fixa, eixo, dobradiça, corrediça, flexível.</summary>
    public IReadOnlyList<int> JointEfficiencyPm { get; init; } = [1000, 300, 300, 300, 600];

    /// <summary>Minutos de trabalho por junção, além do material.</summary>
    public IReadOnlyList<int> JointMinutes { get; init; } = [120, 240, 180, 240, 60];

    /// <summary>Massa de referência de quem usa (kg), para o desempenho memorizado do item.</summary>
    public int ReferenceRiderKg { get; init; } = 65;

    // --- travessia: o que a água custa no caminho ---

    /// <summary>Custo extra, em células de caminhada (‰), de cada célula de água no trajeto a pé: achar o vau, contornar, vadear.</summary>
    public int FordExtraCellsPm { get; init; } = 4000;

    /// <summary>Custo de pôr na água e tirar a embarcação, uma vez por trajeto, em células de caminhada (‰).</summary>
    public int BoardingCellsPm { get; init; } = 1000;

    // --- quem inventa e como ---

    /// <summary>Frustração (minutos perdidos na água) guardada de um mês para o outro (‰).</summary>
    public int FrustrationKeepPm { get; init; } = 700;

    /// <summary>Frustração (minutos) a partir da qual a pessoa pensa em resolver.</summary>
    public int FrustrationThresholdMinutes { get; init; } = 240;

    /// <summary>Chance por mês (‰) de quem está no limiar, com abertura, tempo livre e habilidade médios, tentar algo.</summary>
    public int BaseAttemptChancePm { get; init; } = 2;

    public int MinInventorAgeYears { get; init; } = 14;

    /// <summary>Horas livres por dia de quem trabalha e de quem não trabalha.</summary>
    public int FreeHoursEmployed { get; init; } = 3;
    public int FreeHoursIdle { get; init; } = 8;

    /// <summary>Habilidade de ofício usada para julgar e construir.</summary>
    public int CraftSkillId { get; init; } = 7;

    /// <summary>Variantes concebidas por tentativa.</summary>
    public int VariantsPerAttempt { get; init; } = 8;

    /// <summary>Erro de julgamento (‰) nas propriedades e medidas de quem não tem habilidade nenhuma; cai com a habilidade.</summary>
    public int JudgmentNoisePm { get; init; } = 800;

    /// <summary>Erro de fabricação (‰) nas medidas de quem não tem habilidade nenhuma; cai com a habilidade.</summary>
    public int ManufacturingNoisePm { get; init; } = 300;

    /// <summary>Raio (células) da casa até a água para ter visto o que boia e colhido o que só nasce à beira d'água.</summary>
    public int WaterObservationRadius { get; init; } = 6;

    /// <summary>O que marca quem viu o protótipo falhar (raiva e vergonha) e quem o viu funcionar (alegria). Fica aqui, e não em
    /// <c>PsycheRules.Impacts</c>, para o mundo sem invenção seguir byte a byte o de antes.</summary>
    public LivingWorld.Domain.Cognition.LifeEventImpact FailureImpact { get; init; } = new(30, Anger: 0.3, Shame: 0.3);
    public LivingWorld.Domain.Cognition.LifeEventImpact SuccessImpact { get; init; } = new(65, Joy: 0.7);

    // --- difusão e produção ---

    /// <summary>Chance por mês (‰) de quem sabe ensinar alguém da casa ou um amigo próximo.</summary>
    public int TeachChancePm { get; init; } = 120;

    /// <summary>Afeto (0..100) a partir do qual um amigo de fora da casa aprende.</summary>
    public int TeachAffection { get; init; } = 60;

    /// <summary>Fidelidade (‰) que passa no ensino (sobre a de quem ensina) e na cópia de um item visto em uso.</summary>
    public int TeachFidelityPm { get; init; } = 950;
    public int CopyFidelityPm { get; init; } = 700;

    /// <summary>Chance (‰) de copiar um item visto na cidade em vez de inventar do zero, quando há um.</summary>
    public int CopyPreferencePm { get; init; } = 600;

    /// <summary>Meses que quem viu a construção falhar espera antes de tentar de novo.</summary>
    public int RetryAfterFailureMonths { get; init; } = 6;

    /// <summary>Chance por mês (‰) de quem sabe e não tem a sua construir uma.</summary>
    public int BuildChancePm { get; init; } = 200;

    // --- carga, mercado, estoque, pesca, cópia e lápide (AD-143) ---

    /// <summary>O que uma pessoa carrega a pé atravessando água (kg), quando não tem com quê.</summary>
    public int FootCarryKg { get; init; } = 30;

    /// <summary>Massa de uma unidade de recurso da economia (g), por id de recurso; o que não estiver aqui pesa
    /// <see cref="DefaultUnitGrams"/>. Padrão: comida (1) ≈ ração de grão de um dia; água (2) ≈ 2 litros.</summary>
    public IReadOnlyDictionary<int, int> UnitGrams { get; init; } = new Dictionary<int, int> { [1] = 700, [2] = 2000 };

    public int DefaultUnitGrams { get; init; } = 1000;

    /// <summary>Horas de trabalho por mês que o salário paga: o preço da hora de quem faz para vender.</summary>
    public int WorkHoursPerMonth { get; init; } = 240;

    /// <summary>Parte (‰) do material de uma construção que falhou que volta para o estoque da casa.</summary>
    public int SalvagePm { get; init; } = 500;

    /// <summary>Peixe que a água de uma célula sustenta (kg) e quanto o estoque cresce por mês (‰, logístico).</summary>
    public int FishKgPerWaterCell { get; init; } = 200;
    public int FishRegrowthPm { get; init; } = 50;

    /// <summary>Peixe por hora (g) de quem pesca da margem com as mãos, com a água cheia; a peça multiplica pelo que mede.</summary>
    public int FishGramsPerHour { get; init; } = 1000;

    /// <summary>Dias por mês que quem tem com que pescar sai à água no tempo livre.</summary>
    public int FishingDaysPerMonth { get; init; } = 8;

    /// <summary>Até onde (células) alguém vê de perto a peça de outra casa em uso, e quantos olhares contam por chegada.</summary>
    public int SightRadius { get; init; } = 1;
    public int MaxWatchersPerSighting { get; init; } = 16;

    /// <summary>Quanto (‰) relato vivo de algo que já funcionou e se perdeu aumenta a vontade de tentar de quem o ouviu na cidade.</summary>
    public int RumorAttemptBoostPm { get; init; } = 500;

    public int UnitGramsOf(int resourceId) => UnitGrams.TryGetValue(resourceId, out int g) && g > 0 ? g : DefaultUnitGrams;

    public static IReadOnlyList<MaterialSpec> DefaultMaterials { get; } =
    [
        // madeira (média de folhosa e conífera secas): 600 kg/m³, tração 40 MPa de projeto, tronco 6 m × 0,5 m
        new(1, 600, 40, 700, PartShape.Cylinder, 6000, 500, 30, 3, 6, 120, ColorRgb: 0x8B5A2B, FinishPm: 300, GrainPm: 800),
        // feixe de junco: 200 kg/m³ com o ar dos colmos, fraco, encharca; só à beira d'água
        new(2, 200, 5, 50, PartShape.Cylinder, 3000, 250, 50, 4, 10, 8, NeedsWaterNearby: true, ColorRgb: 0xC8B560, FinishPm: 200, GrainPm: 600),
        // couro cru engordurado: 950 kg/m³, 20 MPa, lâmina de 6 mm que se molda
        new(3, 950, 20, 850, PartShape.Box, 2000, 6, 3, 30, 20, 36, Formable: true, ColorRgb: 0x9C6B4E, FinishPm: 400, GrainPm: 300),
        // resina/piche: só revestimento
        new(4, 1050, 2, 1000, PartShape.Box, 300, 50, 1, 60, 10, 60, CoatingOnly: true, ColorRgb: 0x2B1D0E, FinishPm: 900),
        // pedra: 2600 kg/m³, fraca à tração
        new(5, 2600, 8, 950, PartShape.Box, 800, 400, 60, 2, 60, 1200, ColorRgb: 0x8A8A8A, FinishPm: 200, GrainPm: 400, ConductivityPm: 300),
    ];

    public static InventionRules Default { get; } = new();

    public static InventionRules Disabled { get; } = new() { Enabled = false };

    public MaterialSpec? Material(int id)
    {
        foreach (var m in Materials) if (m.Id == id) return m;
        return null;
    }

    public string? Validate()
    {
        foreach (var m in Materials) if (m.Validate() is { } error) return error;
        if (Materials.Select(m => m.Id).Distinct().Count() != Materials.Count) return "Materials: ids repetidos";
        if (WaterDensityKgM3 <= 0 || GravityMmS2 <= 0 || HumanPowerW <= 0 || WalkPaceMmS <= 0) return "natureza ou corpo inválidos";
        if (JointEfficiencyPm.Count != 5 || JointMinutes.Count != 5) return "JointEfficiencyPm/JointMinutes: 5 valores";
        if (VariantsPerAttempt <= 0 || FrustrationThresholdMinutes <= 0) return "busca ou limiar inválidos";
        return null;
    }
}
