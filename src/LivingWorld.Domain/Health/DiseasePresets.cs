using LivingWorld.Domain.Population.Body;

namespace LivingWorld.Domain.Health;

/// <summary>Doenças de época como dado (AD-137). O motor não conhece nenhuma delas: são perfis do cenário. Meses 0–2 inverno,
/// 6–8 verão. Letalidade = 1 × curva por idade (a curva é a letalidade do caso). Todos os números são aproximados, no meio das
/// faixas lembradas para populações pré-industriais, e precisam de conferência.</summary>
public static class DiseasePresets
{
    private const int Lifelong = 36_000; // 100 anos

    /// <summary>Vila medieval: diarreia de verão, catarro de inverno, febre (tifo e tifoide), tísica, bexigas, sarampo e peste.</summary>
    public static DiseaseRules PreIndustrial { get; } = new(
        Enabled: true,
        Diseases:
        [
            // diarreia e disenteria: água e comida, verão; o desmame é a idade mais perigosa
            new(1, 0.02, 1.0, 7, 90)
            {
                Name = "diarreia", Route = DiseaseRoute.WaterFood, IncubationDays = 1, IncubationJitterDays = 2,
                EnvironmentalIncidenceByAge = AgeCurve.Of((0, 0.8), (0.5, 2.0), (2, 1.2), (5, 0.08), (15, 0.08), (60, 0.2)),
                SeasonByMonth = [0.4, 0.4, 0.5, 0.7, 0.9, 1.1, 1.8, 2.2, 1.9, 1.2, 0.6, 0.4],
                CrewContagionPerDay = 0, CommunityContagionPerContact = 0,
                LethalityByAge = AgeCurve.Of((0, 0.09), (1, 0.02), (4, 0.006), (10, 0.003), (50, 0.005), (65, 0.02), (80, 0.04)),
                MalnutritionLethalityFactor = 2.0, BedriddenShare = 0.5, HealthPenalty = 30,
                LethalityReductionAtFullCare = 0.8, DurationReductionAtFullCare = 0.3,
            },
            // catarro, pneumonia e gripe: inverno; mata bebês e velhos
            new(2, 0.02, 1.0, 10, 120)
            {
                Name = "catarro", Route = DiseaseRoute.Respiratory, IncubationDays = 1, IncubationJitterDays = 3,
                EnvironmentalIncidenceByAge = AgeCurve.Of((0, 0.7), (1, 0.3), (5, 0.05), (15, 0.05), (60, 0.15), (75, 0.25)),
                SeasonByMonth = [2.0, 2.0, 1.4, 1.0, 0.7, 0.5, 0.4, 0.4, 0.5, 0.8, 1.2, 1.7],
                HouseholdContagionPerDay = 0.01, CrewContagionPerDay = 0.002, CommunityContagionPerContact = 0.001,
                LethalityByAge = AgeCurve.Of((0, 0.17), (1, 0.025), (5, 0.005), (15, 0.004), (50, 0.012), (65, 0.045), (80, 0.12)),
                MalnutritionLethalityFactor = 1.5, BedriddenShare = 0.6, HealthPenalty = 35,
                LethalityReductionAtFullCare = 0.7, DurationReductionAtFullCare = 0.2,
            },
            // febre (tifo, tifoide): fim do verão e outono; de cama três semanas, mata adultos
            new(3, 0.005, 1.0, 21, 3600)
            {
                Name = "febre", Route = DiseaseRoute.Vector, IncubationDays = 8, IncubationJitterDays = 6,
                EnvironmentalIncidenceByAge = AgeCurve.Of((0, 0.003), (5, 0.006), (15, 0.008), (60, 0.01)),
                SeasonByMonth = [0.7, 0.7, 0.7, 0.8, 0.9, 1.0, 1.2, 1.4, 1.5, 1.3, 1.0, 0.8],
                CrewContagionPerDay = 0.001, CommunityContagionPerContact = 0.0005,
                LethalityByAge = AgeCurve.Of((0, 0.03), (5, 0.04), (15, 0.09), (50, 0.15), (70, 0.25)),
                MalnutritionLethalityFactor = 1.5, BedriddenShare = 1.0, HealthPenalty = 50,
                LethalityReductionAtFullCare = 0.6, DurationReductionAtFullCare = 0.2,
            },
            // tísica: latência longa, anos de doença em que se trabalha menos; mata o adulto jovem; não deixa imunidade
            new(4, 0.0002, 1.0, 540, 0)
            {
                Name = "tisica", Route = DiseaseRoute.Respiratory, IncubationDays = 120, IncubationJitterDays = 240,
                EnvironmentalIncidenceByAge = AgeCurve.Of((0, 0.0003), (10, 0.0006), (15, 0.0028), (45, 0.0028), (60, 0.0015)),
                CrewContagionPerDay = 0.0002, CommunityContagionPerContact = 0,
                LethalityByAge = AgeCurve.Of((0, 0.15), (15, 0.5), (60, 0.5)),
                MalnutritionLethalityFactor = 1.0, BedriddenShare = 0.25, MildSeverity = 0.6, HealthPenalty = 80,
                LethalityReductionAtFullCare = 0.5,
            },
            // bexigas (varíola): chega de fora a cada poucos anos, mata uma em cada quatro ou cinco crianças que pega, imunidade
            // de vida; deixa marca
            new(5, 0.12, 1.0, 18, Lifelong)
            {
                Name = "bexigas", Route = DiseaseRoute.Contact, IncubationDays = 10, IncubationJitterDays = 4, ContagiousBeforeOnsetDays = 1,
                ContagiousDays = 14, CrewContagionPerDay = 0.03, CommunityContagionPerContact = 0.02, ImportAnnualChance = 0.12,
                SeasonByMonth = [1.2, 1.3, 1.4, 1.4, 1.2, 1.0, 0.8, 0.7, 0.7, 0.7, 0.8, 1.0],
                LethalityByAge = AgeCurve.Of((0, 0.35), (1, 0.25), (5, 0.08), (15, 0.1), (60, 0.25)),
                MalnutritionLethalityFactor = 0.5, BedriddenShare = 1.0, HealthPenalty = 60,
                SequelaChance = 0.3, SequelaFrailtyYears = 2,
                FounderImmunityByAge = AgeCurve.Of((0, 0), (2, 0.3), (5, 0.6), (10, 0.85), (20, 0.95)),
                Avoidance = 0.5, LethalityReductionAtFullCare = 0.3,
            },
            // sarampo: muito contagioso já no pródromo, imunidade de vida; pesa no desnutrido
            new(6, 0.25, 1.0, 10, Lifelong)
            {
                Name = "sarampo", Route = DiseaseRoute.Respiratory, IncubationDays = 9, IncubationJitterDays = 3, ContagiousBeforeOnsetDays = 3,
                ContagiousDays = 5, CrewContagionPerDay = 0.05, CommunityContagionPerContact = 0.04, ImportAnnualChance = 0.15,
                SeasonByMonth = [1.1, 1.3, 1.5, 1.6, 1.4, 1.0, 0.7, 0.6, 0.6, 0.7, 0.8, 0.9],
                LethalityByAge = AgeCurve.Of((0, 0.05), (1, 0.04), (5, 0.012), (15, 0.02)),
                MalnutritionLethalityFactor = 3.0, BedriddenShare = 0.9, HealthPenalty = 40,
                FounderImmunityByAge = AgeCurve.Of((0, 0), (2, 0.4), (5, 0.75), (10, 0.95)),
                LethalityReductionAtFullCare = 0.6,
            },
            // peste: rara, de fora, no verão; mata mais da metade de quem pega, em dias; o medo esvazia o mercado
            new(7, 0.12, 1.0, 7, 3600)
            {
                Name = "peste", Route = DiseaseRoute.Vector, IncubationDays = 3, IncubationJitterDays = 3, ContagiousBeforeOnsetDays = 1,
                CrewContagionPerDay = 0.02, CommunityContagionPerContact = 0.02, ImportAnnualChance = 0.005,
                SeasonByMonth = [0.3, 0.3, 0.4, 0.7, 1.0, 1.4, 1.8, 2.0, 1.8, 1.3, 0.6, 0.4],
                LethalityByAge = AgeCurve.Of((0, 0.65), (5, 0.55), (15, 0.55), (60, 0.65)),
                BedriddenShare = 1.0, HealthPenalty = 70, Avoidance = 0.6, LethalityReductionAtFullCare = 0.2,
            },
        ],
        OutbreakPopulationThreshold: int.MaxValue,
        OutbreakDailyChance: 0)
    {
        // parte da tábua que a doença explica, pela idade: quase toda a morte da criança, metade da do adulto, pouco do velho
        BaselineShareByAge = AgeCurve.Of((0, 0.6), (1, 0.7), (5, 0.65), (15, 0.5), (50, 0.4), (65, 0.25), (80, 0.2)),
        CommunityContactsPerDay = 4,
        WorkCrewSize = 6,
        BedriddenVisitFactor = 0.3,
        CaregiverMinAgeYears = 12,
        CareWorkShare = 0.5,
        CaregiverExposureMultiplier = 2.0,
        UncaredLethalityMultiplier = 1.3,
    };
}
